using HLSTools.NETFramework;
using System;
using System.Collections.Generic;
using System.IO;

namespace HLSWPFTest.NETFramework
{
    public class MediaSegmentBuffer : IDisposable
    {
        private const string MediaSegmentContentFilename = "media{0}.ts";

        /// <summary>
        /// Fired, when a media segment is added into this buffer.
        /// Will provide the number of media segments in the buffer.
        /// </summary>
        public event EventHandler<int> BufferChanged;

        public int CurrentIndex
        {
            get;
            private set;
        }

        private IList<MediaSegmentContent> _mediaSegmentContentList;
        
        public MediaSegmentBuffer()
        {
            _mediaSegmentContentList = new List<MediaSegmentContent>();
        }

        /// <summary>
        /// Adds the given media into the buffer.
        /// </summary>
        /// <param name="mediaSegmentContent">The media to add.</param>
        public void Add(MediaSegmentContent mediaSegmentContent)
        {
            FileWriter.WriteBytesToFile(
                mediaSegmentContent.FullContent,
                string.Format(MediaSegmentContentFilename, _mediaSegmentContentList.Count));

            _mediaSegmentContentList.Add(mediaSegmentContent);

            System.Diagnostics.Debug.WriteLine($"Media segment number {_mediaSegmentContentList.Count} added to buffer");

            BufferChanged?.Invoke(this, _mediaSegmentContentList.Count);
        }

        /// <summary>
        /// Resolves the next media and the associated .ts file that is stored locally.
        /// </summary>
        /// <param name="mediaSegmentContent">The next media.</param>
        /// <param name="localTsFileUri">The .ts file associated with the media segment.</param>
        /// <returns>True, if the method was able to resolve the next media. False otherwise.</returns>
        public bool TryGetNext(out MediaSegmentContent mediaSegmentContent, out Uri localTsFileUri)
        {
            if (_mediaSegmentContentList.Count >= CurrentIndex)
            {
                mediaSegmentContent = _mediaSegmentContentList[CurrentIndex];
                string filename = string.Format(MediaSegmentContentFilename, CurrentIndex);
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), filename);
                localTsFileUri = new Uri(filePath);
                CurrentIndex++;
                return true;
            }

            mediaSegmentContent = null;
            localTsFileUri = null;
            return false;
        }

        public void Dispose(int index)
        {
            if (_mediaSegmentContentList.Count > index
                && _mediaSegmentContentList[index] != null)
            {
                System.Diagnostics.Debug.WriteLine($"Disposing buffered media segment with index {index}");
                _mediaSegmentContentList[index].Dispose();
                _mediaSegmentContentList[index] = null;
            }

            // TODO: Delete TS file
        }

        public void Dispose()
        {
            foreach (MediaSegmentContent mediaSegmentContent in _mediaSegmentContentList)
            {
                mediaSegmentContent.Dispose();
            }

            _mediaSegmentContentList.Clear();
            // TODO: Delete TS files
        }
    }
}
