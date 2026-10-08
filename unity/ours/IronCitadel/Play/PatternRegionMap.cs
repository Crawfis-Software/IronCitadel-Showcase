using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.Play
{
    /// <summary>The room under a world point, for a level that is not on level.json's 45 m grid: the
    /// pattern-tile level. It reads a regions file (written by IronCitadel.Patterns.Editor.IronCitadelPatternsPlay
    /// from the solver's stitched layout): a grid of square cells with its south-west corner at
    /// <c>origin_xz</c>, grouped into slots (one pattern tile each, named after the pattern the solver placed
    /// there), and named areas inside the slots (a neck, a flank, the keep, ...). A point in an area reads
    /// "slot · area"; elsewhere in a slot it reads the slot's name.</summary>
    public sealed class PatternRegionMap : IRoomNames
    {
        [Serializable] class SlotJson { public int[] slot; public string name; public string id; }
        [Serializable] class AreaJson { public string name; public int[] slot; public int[] cells; }
        [Serializable] class RegionsJson
        {
            public string format, id;
            public float[] origin_xz;
            public float cell_m = 5f;
            public int[] cells;
            public int slot_cells = 20;
            public SlotJson[] slots;
            public AreaJson[] areas;
        }

        public string Id { get; private set; }
        public Vector2 Origin { get; private set; }
        public float CellM { get; private set; }
        public int CellsX { get; private set; }
        public int CellsZ { get; private set; }
        public int SlotCells { get; private set; }

        readonly Dictionary<(int, int), (string name, string id)> slotName = new Dictionary<(int, int), (string, string)>();
        readonly Dictionary<(int, int), string> areaAt = new Dictionary<(int, int), string>();

        public static PatternRegionMap Parse(string json)
        {
            var j = JsonUtility.FromJson<RegionsJson>(json);
            var m = new PatternRegionMap
            {
                Id = j.id,
                Origin = j.origin_xz != null && j.origin_xz.Length >= 2 ? new Vector2(j.origin_xz[0], j.origin_xz[1]) : Vector2.zero,
                CellM = j.cell_m > 0f ? j.cell_m : 5f,
                CellsX = j.cells != null && j.cells.Length >= 2 ? j.cells[0] : 0,
                CellsZ = j.cells != null && j.cells.Length >= 2 ? j.cells[1] : 0,
                SlotCells = j.slot_cells > 0 ? j.slot_cells : 20
            };
            if (j.slots != null)
                foreach (var s in j.slots)
                    if (s.slot != null && s.slot.Length >= 2) m.slotName[(s.slot[0], s.slot[1])] = (s.name, s.id);
            // Areas come smallest first in the file, so the first name a cell gets is the most specific one.
            if (j.areas != null)
                foreach (var a in j.areas)
                {
                    if (a.cells == null) continue;
                    for (int i = 0; i + 1 < a.cells.Length; i += 2)
                    {
                        var key = (a.cells[i], a.cells[i + 1]);
                        if (!m.areaAt.ContainsKey(key)) m.areaAt[key] = a.name;
                    }
                }
            return m;
        }

        /// <summary>The cell (x east, z north, from the south-west corner) under a world point.</summary>
        public bool CellAt(Vector3 p, out int x, out int z)
        {
            x = Mathf.FloorToInt((p.x - Origin.x) / CellM);
            z = Mathf.FloorToInt((p.z - Origin.y) / CellM);
            return x >= 0 && x < CellsX && z >= 0 && z < CellsZ;
        }

        /// <summary>"Gallery (great hall) · walkway", "Choke (entry)", ...; "Solid rock" in an unused slot and
        /// "Outside" off the grid. <paramref name="id"/> is the slot's id, e.g. "1,2".</summary>
        public string RoomNameAt(Vector3 p, out string id)
        {
            id = null;
            if (!CellAt(p, out int x, out int z)) return "Outside";
            var slot = (x / SlotCells, z / SlotCells);
            string name = "Unmapped";
            if (slotName.TryGetValue(slot, out var s)) { name = s.name; id = s.id; }
            if (areaAt.TryGetValue((x, z), out var area) && !string.IsNullOrEmpty(area)) name += " · " + area;
            return name;
        }
    }
}
