// Where each session sits on the room × time grid. Pure, so it can be tested without a browser.

export interface GridWindow {
  /** Local "HH:mm". */
  opens: string
  closes: string
  stepMin: number
}

/** The centre's opening hours, as in BookingPolicy (src/BrightPath.Api/appsettings.json). */
export const OPENING: GridWindow = { opens: '09:00', closes: '21:30', stepMin: 15 }

export interface Placement {
  /** 0-based index of the room in the room order. */
  column: number
  /** 0-based grid row the session starts on. */
  rowStart: number
  rowSpan: number
  /** Sessions that overlap in one room sit side by side: this one's lane, out of `lanes`. */
  lane: number
  lanes: number
}

interface Placeable {
  id: string
  roomId: string
  startsAt: string
  endsAt: string
}

/**
 * Minutes since local midnight, read from the "HH:mm" of a local ISO string ("2026-03-06T10:30:00+07:00" → 630).
 * It is not converted through the browser's zone, so the grid looks the same wherever the page is opened.
 */
export function minutesOf(localIso: string): number {
  const match = /T(\d{2}):(\d{2})/.exec(localIso)
  if (!match) {
    throw new Error(`Not a local ISO time: ${localIso}`)
  }
  return Number(match[1]) * 60 + Number(match[2])
}

export function rowCount(window: GridWindow): number {
  return (minutesOf(`T${window.closes}`) - minutesOf(`T${window.opens}`)) / window.stepMin
}

export function layoutDay(
  sessions: Placeable[],
  roomIds: string[],
  window: GridWindow = OPENING,
): Map<string, Placement> {
  const opens = minutesOf(`T${window.opens}`)
  const placements = new Map<string, Placement>()

  for (const [column, roomId] of roomIds.entries()) {
    const inRoom = sessions
      .filter((session) => session.roomId === roomId)
      .map((session) => ({ id: session.id, start: minutesOf(session.startsAt), end: minutesOf(session.endsAt) }))
      .sort((left, right) => left.start - right.start || left.end - right.end)

    // Sessions that overlap, directly or through a chain, form one group. Each takes the first lane that is
    // free at its start, and the whole group shares the same lane count so the cards line up.
    let group: { id: string; lane: number }[] = []
    let laneEnds: number[] = []
    let groupEnd = -1
    const closeGroup = () => {
      for (const member of group) {
        // Every id in the group was placed when it joined the group.
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
        const placed = placements.get(member.id)!
        placements.set(member.id, { ...placed, lane: member.lane, lanes: laneEnds.length })
      }
      group = []
      laneEnds = []
    }

    for (const interval of inRoom) {
      if (interval.start >= groupEnd) {
        closeGroup()
      }
      let lane = laneEnds.findIndex((end) => end <= interval.start)
      if (lane === -1) {
        lane = laneEnds.length
        laneEnds.push(interval.end)
      } else {
        laneEnds[lane] = interval.end
      }
      groupEnd = Math.max(groupEnd, interval.end)
      group.push({ id: interval.id, lane })
      placements.set(interval.id, {
        column,
        rowStart: (interval.start - opens) / window.stepMin,
        rowSpan: (interval.end - interval.start) / window.stepMin,
        lane,
        lanes: 1,
      })
    }
    closeGroup()
  }

  return placements
}
