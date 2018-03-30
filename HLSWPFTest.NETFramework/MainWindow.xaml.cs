using HLSTools.NETFramework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HLSWPFTest.NETFramework
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Insert .m3u8 URL here
        private const string PlaylistUrl = "";

        // Insert your Bing Speech to Text API subscription key here
        private const string BingSpeechToTextApiSubscriptionKey = "";

        private const int MinMediaSegmentBufferSize = 1;
        private const int MaxSingleSubtitleLengthInCharacters = 50;

        private HLSProcessor _hlsProcessor;
        private MediaSegmentBuffer _mediaSegmentBuffer;
        private Timer _subtitleTimer;
        private IList<string> _subtitles;
        private IList<int> _subtitleDurationsInMilliseconds;
        private int _subtitleIndex;
        private bool _mediaStarted;

        public MainWindow()
        {
            InitializeComponent();

            _subtitles = new List<string>();
            _subtitleDurationsInMilliseconds = new List<int>();

            _hlsProcessor = new HLSProcessor(BingSpeechToTextApiSubscriptionKey);
            _hlsProcessor.MediaSegmentProcessed += OnHlsMediaSegmentProcessed;

            _mediaSegmentBuffer = new MediaSegmentBuffer();
            _mediaSegmentBuffer.BufferChanged += OnMediaSegmentBufferChanged;

            Loaded += OnMainWindowLoaded;

            mediaElement.LoadedBehavior = MediaState.Manual;
            mediaElement.MediaEnded += OnMediaElementMediaEnded;
        }

        /// <summary>
        /// Starts processing the playlist.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PlaylistUrl))
            {
                throw new ArgumentNullException("No playlist defined");
            }

            progressBar.IsEnabled = true;

            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                try
                {
                    await _hlsProcessor.StartProcessingAsync(PlaylistUrl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start processing playlist: {ex.Message}");
                }
            }).Start();
        }

        /// <summary>
        /// Plays the next media in the buffer.
        /// </summary>
        private void PlayNext()
        {
            if (_mediaSegmentBuffer.TryGetNext(out MediaSegmentContent mediaSegmentContent, out Uri localTsFileUri))
            {
                System.Diagnostics.Debug.WriteLine($"Playing {localTsFileUri}...");

                mediaElement.Source = localTsFileUri;
                mediaElement.Play();

                ConfigureSubtitlesForNextSegment(mediaSegmentContent);

                _mediaSegmentBuffer.Dispose(_mediaSegmentBuffer.CurrentIndex - 1);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to get the next media segment");
            }
        }

        /// <summary>
        /// Adds the processed media segment into the buffer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="mediaSegmentContent">The processed media segment.</param>
        private void OnHlsMediaSegmentProcessed(object sender, MediaSegmentContent mediaSegmentContent)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                _mediaSegmentBuffer.Add(mediaSegmentContent);
            }));
        }

        /// <summary>
        /// Starts playing the media in the buffer given that we have enough buffered content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="numberOfMediaSegmentsInBuffer"></param>
        private void OnMediaSegmentBufferChanged(object sender, int numberOfMediaSegmentsInBuffer)
        {
            if (!_mediaStarted && numberOfMediaSegmentsInBuffer >= MinMediaSegmentBufferSize)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    progressBar.IsEnabled = false;
                    progressBar.Visibility = Visibility.Collapsed;
                }));

                _mediaStarted = true;
                PlayNext(); 
            }
        }

        private void OnMediaElementMediaEnded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Media ended - trying to play the next one");
            PlayNext();
        }

        /// <summary>
        /// Configures the subtitles for the given media segment.
        /// </summary>
        /// <param name="mediaSegmentContent"></param>
        private void ConfigureSubtitlesForNextSegment(MediaSegmentContent mediaSegmentContent)
        {
            if (_subtitleTimer != null)
            {
                _subtitleTimer.Dispose();
                _subtitleTimer = null;
            }

            _subtitles.Clear();
            _subtitleDurationsInMilliseconds.Clear();
            _subtitleIndex = 0;

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                subtitleTextBlock.Text = string.Empty;
            }));

            if (mediaSegmentContent.Transcription != null
                && mediaSegmentContent.Transcription.NBest.Count > 0
                && !string.IsNullOrWhiteSpace(mediaSegmentContent.Transcription.NBest[0].Display))
            {
                if (mediaSegmentContent.Transcription.NBest[0].Confidence > 0.5f)
                {
                    string transcription = mediaSegmentContent.Transcription.NBest[0].Display;
                    string[] words = transcription.Split(' ');
                    string subtitle = string.Empty;

                    for (int i = 0; i < words.Length; ++i)
                    {
                        subtitle += $"{words[i]} ";

                        if (subtitle.Length >= MaxSingleSubtitleLengthInCharacters
                            || i == words.Length - 1)
                        {
                            _subtitles.Add(subtitle.Trim());
                            subtitle = string.Empty;
                        }
                    }

                    // Transcription.Duration is measured in "ticks"
                    int transcriptionDurationInMilliseconds =
                        mediaSegmentContent.Transcription.Duration / 10000;

                    foreach (string subtitleElement in _subtitles)
                    {
                        double ratio = (double)subtitleElement.Length / (double)transcription.Length;
                        _subtitleDurationsInMilliseconds.Add((int)(transcriptionDurationInMilliseconds * ratio));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No enough confidence in respect to transcription of this segment: {mediaSegmentContent.Transcription.NBest[0].Confidence}");
                }
            }

            if (_subtitleDurationsInMilliseconds.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Resolved {_subtitleDurationsInMilliseconds.Count} subtitle segment(s) for the current media segment");
                SubtitleTimerCallback(null);
            }
        }

        /// <summary>
        /// Updates the subtitle text.
        /// </summary>
        /// <param name="state"></param>
        private void SubtitleTimerCallback(object state)
        {
            if (_subtitles.Count > _subtitleIndex)
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                    subtitleTextBlock.Text = _subtitles[_subtitleIndex];
                }));

                System.Diagnostics.Debug.WriteLine(
                    $"Subtitle segment: {_subtitleDurationsInMilliseconds[_subtitleIndex]} ms -> \"{_subtitles[_subtitleIndex]}\"");
            }

            if (_subtitleDurationsInMilliseconds.Count > _subtitleIndex + 1)
            {
                // We have a next subtitle for this segment
                if (_subtitleTimer != null)
                {
                    _subtitleTimer.Dispose();
                }

                _subtitleTimer = new Timer(
                    SubtitleTimerCallback,
                    null,
                    _subtitleDurationsInMilliseconds[_subtitleIndex],
                    int.MaxValue);
            }

            _subtitleIndex++;
        }
    }
}
