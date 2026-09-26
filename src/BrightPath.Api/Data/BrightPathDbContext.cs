using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Data;

public sealed class BrightPathDbContext(DbContextOptions<BrightPathDbContext> options) : DbContext(options);
