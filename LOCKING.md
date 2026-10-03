# Hạn chế của cách khóa hiện tại

`TRANSACTIONS.md` giải thích **vì sao** các endpoint ghi phải mở transaction và khóa. File này ghi lại **những điểm yếu** của cách đang làm, để người sửa code sau biết chỗ nào dễ hỏng.

## Cách đang làm

Có hai loại khóa, cả hai đều chỉ được giữ **đến khi transaction kết thúc**:

| Khóa | Nơi gọi | Dùng trong |
|---|---|---|
| Khóa dòng `SELECT * FROM sessions WHERE id = … FOR UPDATE` | `SessionRepository.GetForUpdateAsync` (Infrastructure) | Cancel, Move |
| Advisory lock `pg_advisory_xact_lock` theo (tutor, ngày) | `PostgresBookingLocks.TutorDayAsync` (Infrastructure) | Create, Move |

Transaction thì được mở ở chỗ khác, trong từng handler (Application):

```csharp
await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
var session = await sessions.GetForUpdateAsync(id, ct);   // khóa
...
await unitOfWork.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);                         // nhả khóa
```

## 1. Khóa và transaction nằm ở hai nơi

Khóa có tác dụng hay không là do **caller** quyết định, không phải hàm khóa. `GetForUpdateAsync` và `TutorDayAsync` âm thầm giả định caller đã mở transaction, nhưng không có gì trong chữ ký hàm bắt buộc điều đó. Chỉ có doc comment trên `ISessionRepository` nhắc: *"locked until the transaction ends"*.

Hậu quả: đọc riêng handler thì không thấy khóa cần transaction, còn đọc riêng repository thì không thấy transaction được mở ở đâu. Muốn hiểu phải đọc cả hai.

## 2. Quên mở transaction thì hỏng mà không báo lỗi

Nếu một handler mới gọi `GetForUpdateAsync` hoặc `TutorDayAsync` mà không gọi `BeginTransactionAsync` trước:

- Câu `SELECT … FOR UPDATE` chạy ở chế độ autocommit, tự commit ngay, **khóa bị nhả luôn**.
- Không có exception, không có warning. Code vẫn chạy đúng khi chỉ có một người dùng.
- Race (write skew ở Cancel, đếm sai số buổi của tutor ở Create/Move) chỉ lộ ra khi hai request chạy đồng thời. Cái này gần như không bao giờ thấy khi test tay.

Tương tự, nếu `SaveChangesAsync` hoặc `CommitAsync` bị đẩy ra ngoài khối transaction, hoặc ai đó commit sớm giữa chừng, thì phần phía sau chạy mà không có khóa.

## 3. Unit test không bắt được lỗi này

`tests/BrightPath.Api.UnitTests/Fakes/InMemoryBooking.cs` giả lập mọi port trong bộ nhớ:

- `GetForUpdateAsync` chỉ trả về session, không khóa gì.
- `TutorDayAsync` là `Task.CompletedTask`.
- `BeginTransactionAsync` trả về một transaction giả, chỉ đếm `Commits`.

Fake này không biết khóa đang được gọi trong hay ngoài transaction. Handler quên `BeginTransactionAsync` vẫn pass hết unit test.

Chỉ có integration test chạy trên Postgres thật mới kiểm được. `MoveSessionTests` và `CancelAttendeeTests` giữ khóa ở một connection khác rồi chờ API bị chặn (`ApiCalls.WaitUntilBlocked`). Nhưng các test này chỉ phủ **những endpoint đã có**. Endpoint mới phải tự viết test tương tự, và rất dễ quên.

## 4. Thứ tự khóa chỉ là quy ước

Move lấy khóa dòng của session cũ **trước**, rồi mới lấy advisory lock (tutor, ngày mới). Create chỉ lấy advisory lock. Cancel chỉ lấy khóa dòng. Với ba luồng này thì không có vòng chờ nên không deadlock.

Nhưng không có gì bắt buộc thứ tự đó. Một use case mới lấy advisory lock trước rồi mới `FOR UPDATE` một session có thể deadlock với Move. Postgres sẽ phát hiện và hủy một transaction (`40P01 deadlock_detected`), và lỗi đó hiện chưa được xử lý nên sẽ thành 500.

## 5. Chờ khóa không có giới hạn thời gian

Không có `lock_timeout`, `statement_timeout` hay `NOWAIT` nào được cấu hình. Request thứ hai chờ khóa cho đến khi request thứ nhất commit, dù lâu bao nhiêu. Nếu một transaction bị treo (ví dụ đang giữ khóa mà chờ một call chậm), các request khác trên cùng session hoặc cùng (tutor, ngày) cũng treo theo, cho đến khi client hủy request (`CancellationToken`).

Hiện tại thì trong transaction không có call ra ngoài và các transaction đều ngắn, nên chưa thành vấn đề. Nhưng đây cũng chỉ là quy ước.

## 6. Rollback dựa vào `await using`

Các nhánh `return` sớm (404, 409, bắt `SlotTakenException`) không gọi rollback một cách tường minh. Transaction được rollback và nhả khóa là nhờ `await using` dispose nó. Nếu ai đó viết `var transaction = await …` mà thiếu `await using`, transaction sẽ còn mở cho đến khi `DbContext` của request bị dispose. Trong lúc đó khóa vẫn bị giữ.

## 7. Raw SQL gắn chặt với Postgres và schema

- `FOR UPDATE` và `pg_advisory_xact_lock` là cú pháp riêng của Postgres. Đổi database là phải viết lại, và fake trong unit test không thể giả lập được.
- Tên bảng `sessions` được viết cứng trong chuỗi SQL. Đổi tên bảng trong mapping mà quên câu này thì chỉ lỗi lúc chạy, không lỗi lúc build.
- `SELECT *` yêu cầu các cột trả về khớp với mapping của `Session`. Cột mới chưa map thì EF bỏ qua, nhưng đổi tên cột mà không sửa mapping thì truy vấn lỗi lúc chạy.

## Hướng khắc phục có thể làm

Chưa làm, ghi lại để cân nhắc:

1. **Guard fail-fast** (rẻ nhất): trong `GetForUpdateAsync` và `TutorDayAsync`, nếu `db.Database.CurrentTransaction is null` thì throw `InvalidOperationException`. Quên transaction sẽ thành lỗi rõ ràng ngay ở lần chạy đầu tiên, kể cả khi chỉ có một người dùng. Cách này xử lý được hạn chế 2. Hạn chế 1 vẫn còn, nhưng không còn nguy hiểm.
2. **Gom transaction vào một chỗ**: thêm `IUnitOfWork.InTransactionAsync(Func<CancellationToken, Task<Result<T>>>)`. Hàm này mở transaction, commit khi `Result` thành công, rollback khi lỗi. Handler không còn tự gọi `BeginTransactionAsync`/`CommitAsync`, xử lý được hạn chế 1 và 6. Đổi lại phải sửa ba handler, và code bên trong lambda khó đọc hơn một chút.
3. **Fake biết về transaction**: cho `InMemoryBooking` ghi nhận transaction đang mở, và throw nếu `GetForUpdateAsync`/`TutorDayAsync` được gọi ngoài transaction. Unit test sẽ bắt được hạn chế 3.
4. **Đặt `lock_timeout`** (ví dụ vài giây) cho các transaction ghi, và map lỗi `55P03 lock_not_available` / `40P01 deadlock_detected` thành 409 thay vì 500. Xử lý hạn chế 4 và 5.
