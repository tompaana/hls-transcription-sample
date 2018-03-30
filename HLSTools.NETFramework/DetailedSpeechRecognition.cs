using System;
using System.Collections.Generic;

namespace HLSTools.NETFramework
{
    public class NBest
    {
        public double Confidence { get; set; }
        public string Lexical { get; set; }
        public string ITN { get; set; }
        public string MaskedITN { get; set; }
        public string Display { get; set; }
    }

    /// <summary>
    /// For deserializing the transcription JSON result.
    /// </summary>
    public class DetailedSpeechRecognition
    {
        public string RecognitionStatus { get; set; }
        public int Offset { get; set; }
        public int Duration { get; set; }
        public List<NBest> NBest { get; set; }

        public DetailedSpeechRecognition()
        {
            NBest = new List<NBest>();
        }
    }
}
