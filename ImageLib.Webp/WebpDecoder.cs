﻿using ImageLib.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using WebpLib;

namespace ImageLib.Webp
{
    public class WebpDecoder : IImageDecoder
    {

        public int HeaderSize
        {
            get
            {
                return 12;
            }
        }

        public void Dispose()
        {
            //empty
        }

        public async Task<ImageSource> InitializeAsync(CoreDispatcher dispatcher, IRandomAccessStream streamSource, CancellationTokenSource cancellationTokenSource)
        {
            byte[] bytes = new byte[streamSource.Size];
            await streamSource.ReadAsync(bytes.AsBuffer(), (uint)streamSource.Size, InputStreamOptions.None).AsTask(cancellationTokenSource.Token);
            var imageSource = WebpCodec.Decode(bytes);
            return imageSource;
        }

        public bool IsSupportedFileFormat(byte[] header)
        {
            return header != null && header.Length == 12
                && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P';
        }

        public ImageSource RecreateSurfaces()
        {
            return null;
        }

        public void Start()
        {
            //empty
        }

        public void Stop()
        {
            //empty
        }
    }
}