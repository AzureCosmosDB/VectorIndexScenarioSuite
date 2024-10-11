using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VectorIndexScenarioSuite
{
    /*
     * This is a utility class to keep track of TagId and its corresponding VectorId.
     * The concept of Tags and VectorIds are used in the streaming scenario defined by Big-Ann-Benchmarks below:
     * https://github.com/harsha-simhadri/big-ann-benchmarks/tree/main/neurips23/streaming
     */
    internal class TagIdMapper
    {
        private Dictionary<Tuple<int, int>, Tuple<int, int>> tagRangeToVectorIdRange;

        public TagIdMapper()
        {
            this.tagRangeToVectorIdRange = new Dictionary<Tuple<int, int>, Tuple<int, int>>();
        }

        public void AddTagIdMapping(int tagIdStart, int tagIdEnd, int vectorIdStart, int vectorIdEnd)
        {
            if(tagIdEnd <= tagIdStart || vectorIdEnd <= vectorIdStart)
            {
                throw new ArgumentException("Invalid range provided for TagId or VectorId.");
            }

            this.tagRangeToVectorIdRange.Add(
                new Tuple<int, int>(tagIdStart, tagIdEnd), new Tuple<int, int>(vectorIdStart, vectorIdEnd));
        }

        public int GetVectorIdForTagId(int tagId)
        {
            foreach (var tagRange in this.tagRangeToVectorIdRange.Keys)
            {
                if (tagId >= tagRange.Item1 && tagId <= tagRange.Item2)
                {
                    int delta = tagId - tagRange.Item1;
                    return this.tagRangeToVectorIdRange[tagRange].Item1 + delta;
                }
            }

            // If no mapping found, this implies no replaces were done, so return the same tagId as vectorId.
            return tagId;
        }
    }
}
