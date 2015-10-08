﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Linq;
using ImageLib.IO;

namespace ImageLib.Controls
{
    public sealed partial class ImageView : UserControl
    {
        #region Public Events
        public event EventHandler LoadingStarted;
        public event EventHandler<LoadingCompletedEventArgs> LoadingCompleted;
        public event EventHandler<Exception> LoadingFailed;
        #endregion

        public static DependencyProperty StretchProperty { get; } = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(ImageView),
            new PropertyMetadata(Stretch.None)
            );

        public static DependencyProperty UriSourceProperty { get; } = DependencyProperty.Register(
            nameof(UriSource),
            typeof(Uri),
            typeof(ImageView),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSourcePropertyChanged))
            );

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public Uri UriSource
        {
            get { return (Uri)GetValue(UriSourceProperty); }
            set { SetValue(UriSourceProperty, value); }
        }

        private IImageDecoder _imageDecoder;
        private bool _isLoaded;
        private CancellationTokenSource _initializationCancellationTokenSource;

        public ImageView()
        {
            this.InitializeComponent();

            this.Loaded += ((s, e) =>
            {
                // 注册事件（VisibilityChanged），当最小化的时候停止动画。
                Window.Current.VisibilityChanged += OnVisibilityChanged;
                // Register for SurfaceContentsLost to recreate the image source if necessary
                CompositionTarget.SurfaceContentsLost += OnSurfaceContentsLost;
            });
            this.Unloaded -= ((s, e) =>
            {
                // 解注册事件
                Window.Current.VisibilityChanged += OnVisibilityChanged;
                CompositionTarget.SurfaceContentsLost -= OnSurfaceContentsLost;

            });

        }

        private async static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var that = d as ImageView;
            await that?.UpdateSourceAsync();
        }
        private static DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static long CurrentTimeMillis(DateTime d)
        {
            return (long)((DateTime.UtcNow - Jan1st1970).TotalMilliseconds);
        }
        private async Task UpdateSourceAsync()
        {

            _imageDecoder?.Stop();
            _initializationCancellationTokenSource?.Cancel();

            _image.Source = null;
            _imageDecoder = null;

            if (UriSource != null)
            {
                var uriSource = UriSource;
                var cancellationTokenSource = new CancellationTokenSource();

                _initializationCancellationTokenSource = cancellationTokenSource;

                try
                {
                    this.OnLoadingStarted();

                    var streamReference = RandomAccessStreamReference.CreateFromUri(uriSource);
                    var readStream = await streamReference.OpenReadAsync().AsTask(cancellationTokenSource.Token);
                    ImageSource imageSource = null;
                    bool hasDecoder = false;
                    var decoders = Decoders.GetAvailableDecoders();
                    if (decoders.Count > 0)
                    {
                        int maxHeaderSize = decoders.Max(x => x.HeaderSize);
                        if (maxHeaderSize > 0)
                        {
                            byte[] header = new byte[maxHeaderSize];
                            await readStream.AsStreamForRead().ReadAsync(header, 0, maxHeaderSize);
                            var decoder = decoders.FirstOrDefault(x => x.IsSupportedFileFormat(header));
                            if (decoder != null)
                            {
                                imageSource = await decoder.InitializeAsync(readStream);
                                _imageDecoder = decoder;
                                if (_isLoaded)
                                {
                                    _imageDecoder.Start();
                                }
                                hasDecoder = true;
                            }
                        }
                    }
                    if (!hasDecoder)
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(readStream).AsTask(_initializationCancellationTokenSource.Token);
                        imageSource = bitmapImage;
                    }
                    _image.Source = imageSource;
                    //暂时未实现
                    this.OnLoadingCompleted(0, 0);

                }
                catch (TaskCanceledException)
                {
                    // Just keep the empty image source.
                }
                catch (FileNotFoundException fnfex)
                {
                    this.OnFail(fnfex);
                }
                catch (Exception ex)
                {
                    this.OnFail(ex);
                }
            }

        }


        private void OnLoadingStarted()
        {
            if (LoadingStarted != null)
            {
                LoadingStarted.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnLoadingCompleted(double width, double height)
        {
            if (LoadingCompleted != null)
            {
                LoadingCompleted.Invoke(this, new LoadingCompletedEventArgs(width, height));
            }
        }

        private void OnFail(Exception ex)
        {
            if (LoadingFailed != null)
            {
                LoadingFailed.Invoke(this, ex);
            }
        }

        #region 控件生命周期

        private void OnVisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                _imageDecoder?.Start();
            }
            else if (!e.Visible)
            {
                _imageDecoder?.Stop(); // Prevent unnecessary work
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            _imageDecoder?.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            _imageDecoder?.Stop();
        }

        private void OnSurfaceContentsLost(object sender, object e)
        {
            _image.Source = _imageDecoder?.RecreateSurfaces();
        }

        #endregion


    }
}
