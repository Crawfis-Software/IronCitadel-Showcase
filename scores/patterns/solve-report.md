# Solve report: iron-citadel

Grid 3 x 4, walk "explicit", 6 slot(s) on the walk, 0 branch slot(s), in_order=True. Steps are counted along the walk from 1.

## The wants: promised against measured

| want (asked order) | promised at | measured there | measured on the walk at | measured off the walk |
|---|---|---|---|---|
| choke | step 1 (1,0) | yes | step 1 (1,0), step 3 (0,1), step 5 (1,2) |  |
| flanking-route | step 3 (0,1) | yes | step 3 (0,1) |  |
| hidden-cache | step 4 (0,2) | yes | step 4 (0,2), step 5 (1,2), step 6 (1,3) |  |
| gallery | step 5 (1,2) | yes | step 5 (1,2) |  |
| stronghold | step 6 (1,3) | yes | step 6 (1,3) |  |

## The walk, slot by slot

| step | slot | tile | the tile says it holds | the measured level shows there |
|---|---|---|---|---|
| 1 | (1,0) | d-20-flanking-route-003@1 | choke, collision-point, flanking-route, foreshadowing | choke |
| 2 | (1,1) | d-20-sniper-location-013@3 | collision-point, sniper-location | foldback-loop, sniper-location |
| 3 | (0,1) | d-20-flanking-route-010@3 | choke, collision-point, flanking-route, foreshadowing | choke, collision-point, flanking-route |
| 4 | (0,2) | d-20-hidden-cache-005@3 | collision-point, hidden-cache | hidden-cache |
| 5 | (1,2) | d-20-gallery-010@1 | collision-point, foreshadowing, gallery, sniper-location | choke, foldback-loop, foreshadowing, gallery, hidden-cache, sniper-location |
| 6 | (1,3) | d-20-stronghold-007@0 | collision-point, foreshadowing, stronghold | arena, foldback-loop, foreshadowing, hidden-cache, stronghold |

The measured walk itself steps on, in order: choke, flanking-route, foldback-loop, collision-point, foreshadowing, arena, stronghold. (A pattern that stands beside the walk -- a sniper's perch -- is never stepped on; look for it in the table above.)

Totals: 5 want(s) asked; 5 promised by the tiles; 5 measured where promised; 5 measured somewhere on the walk; 5 readable off the walk in the asked order.
