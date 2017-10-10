using System.Collections.Generic;

namespace GPSTrackerApp {
    public sealed class LineQueue {
        /**
         * This class provides functionality to split strings into lines (handling incomplete lines)
         * and iterate over them.
         * 
         * When an incomplete line is found the class waits extra input to complete the line before
         * returning it.
         */

        private Queue<string> ChunkQueue = new Queue<string>();

        public bool HasMore() {
            if (ChunkQueue.Count == 0) {
                return false;
            }
            /* If the first string in the queue doesn't end with \n then it is unfinished */
            var s = ChunkQueue.Peek();
            if (!s.EndsWith("\n")) {
                return false;
            }
            return true;
        }

        public void AddChunk(string chunk) {
            var lines = chunk.Split('\n');
            for (var i = 0; i < lines.Length; i++) {
                /* Is there an unfinished line in the queue */
                if (!HasMore() && ChunkQueue.Count > 0) {
                    ChunkQueue.Enqueue(ChunkQueue.Dequeue() + lines[i] + "\n");
                    continue;
                }
                /* Handle the case where the last line is incomplete */
                if (i == lines.Length - 1 && !chunk.EndsWith("\n")) {
                    ChunkQueue.Enqueue(lines[i]);
                    continue;
                }
                /* Add \n to the end of the complete line due split removing it */
                ChunkQueue.Enqueue(lines[i] + "\n");
            }
        }

        public string Next() {
            if (!HasMore()) {
                return null;
            }
            return ChunkQueue.Dequeue();
        }
    }
}
