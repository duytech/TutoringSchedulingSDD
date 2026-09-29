import { describe, expect, it } from 'vitest'
import { layoutDay, minutesOf, OPENING, rowCount } from './layout'

const rooms = ['R1', 'R2', 'R3', 'R4', 'R5', 'R6']

function session(id: string, roomId: string, start: string, end: string) {
  return { id, roomId, startsAt: `2026-03-06T${start}:00+07:00`, endsAt: `2026-03-06T${end}:00+07:00` }
}

describe('minutesOf', () => {
  it('reads the local time from the string, whatever the offset', () => {
    expect(minutesOf('2026-03-06T10:30:00+07:00')).toBe(630)
    expect(minutesOf('2026-03-06T10:30:00Z')).toBe(630)
  })
})

describe('layoutDay', () => {
  it('covers the opening hours in 15-minute rows', () => {
    expect(rowCount(OPENING)).toBe(50) // 09:00 to 21:30
  })

  it('puts a 09:00 start on the first row and a 10:30 start six rows down', () => {
    const layout = layoutDay([session('a', 'R1', '09:00', '10:00'), session('b', 'R1', '10:30', '12:00')], rooms)

    expect(layout.get('a')).toMatchObject({ rowStart: 0, rowSpan: 4 })
    expect(layout.get('b')).toMatchObject({ rowStart: 6, rowSpan: 6 })
  })

  it('takes the column from the room order', () => {
    const layout = layoutDay([session('a', 'R3', '09:00', '10:00')], rooms)

    expect(layout.get('a')?.column).toBe(2)
  })

  it('puts two overlapping sessions in one room side by side', () => {
    const layout = layoutDay(
      [session('cancelled', 'R3', '09:00', '10:30'), session('rebooked', 'R3', '09:00', '10:00')],
      rooms,
    )

    expect(layout.get('cancelled')).toMatchObject({ lanes: 2 })
    expect(layout.get('rebooked')).toMatchObject({ lanes: 2 })
    expect(new Set([layout.get('cancelled')?.lane, layout.get('rebooked')?.lane])).toEqual(new Set([0, 1]))
  })

  it('goes back to one lane after the overlap ends', () => {
    const layout = layoutDay(
      [session('a', 'R3', '09:00', '10:30'), session('b', 'R3', '09:00', '10:00'), session('c', 'R3', '10:30', '11:30')],
      rooms,
    )

    expect(layout.get('c')).toMatchObject({ lane: 0, lanes: 1 })
  })

  it('does not share lanes across rooms', () => {
    const layout = layoutDay([session('a', 'R1', '09:00', '10:00'), session('b', 'R2', '09:00', '10:00')], rooms)

    expect(layout.get('a')).toMatchObject({ column: 0, lane: 0, lanes: 1 })
    expect(layout.get('b')).toMatchObject({ column: 1, lane: 0, lanes: 1 })
  })
})
