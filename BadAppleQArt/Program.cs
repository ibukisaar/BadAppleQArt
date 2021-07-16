using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using QArt.NET;

namespace BadAppleQArt {
    class Program {
        const string SourceImagesPath = @"F:\badapple";
        static readonly Regex lyricsRegex = new Regex(@"^(\d+\.\d{2}) (.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

        unsafe static void Main(string[] args) {
            LyricLine[] lyrics = GetLyricLines();

            int version = 20;
            QREcLevel ecLevel = QREcLevel.L;
            QRLayout layout = QRLayout.GetLayout(version, ecLevel);
            int qrSize = layout.Size;

            Bitmap canvas = new Bitmap(1920, 1080);
            Bitmap lyricsCanvas = new Bitmap(840, 1080);
            const int QRPadding = 55;
            const int QRCellSize = 10;
            const int StartX = 0, StartY = 12;
            const int W = 96, H = 72;
            int beginX = Math.Max(-StartX, 0);
            int beginY = Math.Max(-StartY, 0);
            int endX = Math.Min(W, qrSize - StartX);
            int endY = Math.Min(H, qrSize - StartY);
            MagicBit black = new MagicBit(MagicBitType.Expect, true);
            MagicBit white = new MagicBit(MagicBitType.Expect, false);
            MagicBit freedomBlack = new MagicBit(MagicBitType.Freedom, true);
            MagicBit freedomWhite = new MagicBit(MagicBitType.Freedom, false);
            var random = new Random();
            // var freedomCount = QRHelper.GetTotalEccBytes(version, ecLevel);
            int freedomCount = 500;

            bool[] encodedData = null;
            var qrImage = new MagicBit[layout.Map2D.Length];
            Array.Fill(qrImage, new MagicBit(MagicBitType.Freedom, false));
            ref MagicBit imageFirst = ref MemoryMarshal.GetArrayDataReference(qrImage);

            //for (int y = 0; y < qrSize; y++) {
            //    for (int x = 0; x < qrSize; x++) {
            //        if (StartX - x is >= 0 and <= 2) {
            //            if (random.NextDouble() > (StartX - x + 1) / 3.0) {

            //            }
            //        }
            //    }
            //}

            int currentLyricIndex = -1;

            using Graphics gr = Graphics.FromImage(canvas);
            gr.Clear(Color.White);
            string[] images = Directory.GetFiles(SourceImagesPath, "*.png");
            Array.Sort(images);
            string outDir = Path.Combine(SourceImagesPath, "out");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            var drawRects = new List<Rectangle>();

            for (int imageIndex = 0; imageIndex < images.Length; imageIndex++) {
                TimeSpan currentTime = TimeSpan.FromSeconds(imageIndex / 30.0);
                int lyricIndex = currentLyricIndex + 1;
                if (lyricIndex < lyrics.Length && currentTime >= lyrics[lyricIndex].Time) {
                    DrawLyrics(lyricsCanvas, lyrics, lyricIndex);
                    gr.DrawImageUnscaled(lyricsCanvas, new Point(canvas.Width - lyricsCanvas.Width, 0));

                    string text = lyrics[lyricIndex].Text;
                    if (string.IsNullOrWhiteSpace(text)) text = "https://space.bilibili.com/185316";

                    encodedData = QREncoder.Encode(Encoding.UTF8.GetBytes(text), version, ecLevel);
                    encodedData = QREncoder.ToFinalEncodedData(layout, encodedData);

                    currentLyricIndex = lyricIndex;
                }


                using Bitmap originalImage = new Bitmap(images[imageIndex]);
                using Bitmap bitmap = new Bitmap(originalImage, new Size(W, H));
                //using Bitmap smallBitmap = new Bitmap(bitmap, new Size(W / 24, H / 24));
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                //var smallBitmapData = smallBitmap.LockBits(new Rectangle(0, 0, W / 24, H / 24), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                //var smallPointer = (uint*)smallBitmapData.Scan0;
                const double MutationsProbability = 0.055;
                int blackCount = 0, whiteCount = 0;

                for (int y = beginY; y < endY; y++) {
                    uint* p = (uint*)bitmapData.Scan0 + y * W + beginX;
                    for (int x = beginX; x < endX; x++, p++) {
                        byte b = ((byte*)p)[0];
                        byte g = ((byte*)p)[1];
                        byte r = ((byte*)p)[2];
                        if ((b + g + r) > 384) {
                            whiteCount++;
                        } else {
                            blackCount++;
                        }
                    }
                }

                static double GetMutationsProbability(double value) {
                    double a = 7.5 * (value - 0.5);
                    double b = Math.Exp(a);
                    return ((b - 1 / b) / (b + 1 / b) + 1) * MutationsProbability;
                }

                double blackMutationsProbability = GetMutationsProbability(blackCount / (double)(blackCount + whiteCount));
                double whiteMutationsProbability = GetMutationsProbability(whiteCount / (double)(blackCount + whiteCount));

                for (int y = beginY; y < endY; y++) {
                    uint* p = (uint*)bitmapData.Scan0 + y * W + beginX;
                    int imgOffset = (StartY + y) * qrSize + StartX;
                    for (int x = beginX; x < endX; x++, p++) {
                        //uint* sp = smallPointer + (y / 24) * (W / 24) + (x / 24);
                        //byte b = ((byte*)p)[0];
                        //byte g = ((byte*)p)[1];
                        //byte r = ((byte*)p)[2];
                        //if ((b + g + r) > 384) {
                        //    b = ((byte*)sp)[0];
                        //    g = ((byte*)sp)[1];
                        //    r = ((byte*)sp)[2];
                        //    if ((b + g + r) > 384 && random.NextDouble() < MutationsProbability) {
                        //        Unsafe.Add(ref imageFirst, imgOffset + x) = freedomBlack;
                        //    } else {
                        //        Unsafe.Add(ref imageFirst, imgOffset + x) = white;
                        //    }
                        //} else {
                        //    b = ((byte*)sp)[0];
                        //    g = ((byte*)sp)[1];
                        //    r = ((byte*)sp)[2];
                        //    if ((b + g + r) <= 384 && random.NextDouble() < MutationsProbability) {
                        //        Unsafe.Add(ref imageFirst, imgOffset + x) = freedomWhite;
                        //    } else {
                        //        Unsafe.Add(ref imageFirst, imgOffset + x) = black;
                        //    }
                        //}

                        byte b = ((byte*)p)[0];
                        byte g = ((byte*)p)[1];
                        byte r = ((byte*)p)[2];
                        if ((b + g + r) > 384) {
                            if (random.NextDouble() < whiteMutationsProbability) {
                                Unsafe.Add(ref imageFirst, imgOffset + x) = freedomBlack;
                            } else {
                                Unsafe.Add(ref imageFirst, imgOffset + x) = white;
                            }
                            //Unsafe.Add(ref imageFirst, imgOffset + x) = white;
                        } else {
                            if (random.NextDouble() < blackMutationsProbability) {
                                Unsafe.Add(ref imageFirst, imgOffset + x) = freedomWhite;
                            } else {
                                Unsafe.Add(ref imageFirst, imgOffset + x) = black;
                            }
                            //Unsafe.Add(ref imageFirst, imgOffset + x) = black;
                        }
                    }
                }

                QRMapInfo** layoutMap = layout.Map2D.Pointer;
                //for (int y = beginY; y < endY; y++) {
                //    int imgOffset = (StartY + y) * qrSize + StartX;
                //    for (int x = beginX; x < endX; x++) {
                //        if (layoutMap[imgOffset + x]->Type is QRType.AlignmentPatterns) {
                //            Unsafe.Add(ref imageFirst, imgOffset + x - qrSize - 1) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x - qrSize) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x - qrSize + 1) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x - 1) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x + 1) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x + qrSize - 1) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x + qrSize) = freedomWhite;
                //            Unsafe.Add(ref imageFirst, imgOffset + x + qrSize + 1) = freedomWhite;
                //        }
                //    }
                //}



                bitmap.UnlockBits(bitmapData);
                //smallBitmap.UnlockBits(smallBitmapData);

                //for (int i = 0; i < freedomCount;) {
                //    int offset = random.Next(qrImage.Length);
                //    ref MagicBit bit = ref Unsafe.Add(ref imageFirst, offset);
                //    if (bit.Type is MagicBitType.Freedom) continue;
                //    bit = new MagicBit(MagicBitType.Freedom, false);
                //    i++;
                //}

                QRCodeMagician.WriteDataToImage(layout, encodedData, qrImage);
                var qr = QRCodeMagician.ImageArt(layout, qrImage);

                {
                    gr.FillRectangle(Brushes.White, new Rectangle(QRPadding, QRPadding, qrSize * QRCellSize, qrSize * QRCellSize));

                    drawRects.Clear();
                    QRValue* map = qr.Values.Pointer;
                    nint offset = 0;
                    for (int y = 0; y < qrSize; y++) {
                        for (int x = 0; x < qrSize; x++, offset++) {
                            if (layoutMap[offset]->Type is QRType.AlignmentPatterns or QRType.TimingPatterns && x >= StartX && x < StartX + W && y >= StartY && y < StartY + H) {
                                if (Unsafe.Add(ref imageFirst, offset).Value) {
                                    drawRects.Add(new Rectangle(QRPadding + x * QRCellSize, QRPadding + y * QRCellSize, QRCellSize, QRCellSize));
                                }
                                continue;
                            }
                            if (map[offset].IsBlack) {
                                drawRects.Add(new Rectangle(QRPadding + x * QRCellSize, QRPadding + y * QRCellSize, QRCellSize, QRCellSize));
                            }
                        }
                    }



                    gr.FillRectangles(Brushes.Black, drawRects.ToArray());
                }

                gr.Flush();


                canvas.Save(Path.Combine(outDir, Path.GetFileName(images[imageIndex])));

                Console.Write($"\r{imageIndex + 1}/{images.Length}");
            }
        }


        static LyricLine[] GetLyricLines() {
            var matches = lyricsRegex.Matches(BadAppleLyrics.Text);
            var lyrics = new LyricLine[matches.Count];
            for (int i = 0; i < lyrics.Length; i++) {
                Match m = matches[i];
                TimeSpan time = TimeSpan.FromSeconds(double.Parse(m.Groups[1].Value));
                string lyricText = m.Groups[2].Value.Trim();
                lyrics[i] = new LyricLine(time, lyricText);
            }
            return lyrics;
        }

        static void DrawLyrics(Bitmap lyricsCanvas, LyricLine[] lyrics, int highlightLine) {
            using Graphics g = Graphics.FromImage(lyricsCanvas);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.White);

            for (int i = 0; i < lyrics.Length; i++) {
                if (Math.Abs(i - highlightLine) > 7) continue;

                LyricLine lyric = lyrics[i];
                if (string.IsNullOrEmpty(lyric.Text)) continue;

                using var path = new GraphicsPath();
                path.AddString(lyric.Text, new FontFamily("YaHei Consolas Hybrid"), (int)FontStyle.Regular, 50, default(Point), null);
                RectangleF rectF = path.GetBounds();
                int height = 72;
                int width = (int)Math.Round(rectF.Width + rectF.X * 2);

                int drawX = (lyricsCanvas.Width - width) / 2;
                int drawY = (i - highlightLine + 7) * 72;

                g.Transform = new Matrix();
                g.TranslateTransform(drawX, drawY);

                if (i == highlightLine) {
                    g.FillRectangle(Brushes.Black, new Rectangle(0, 0, width, height));
                    g.FillPath(Brushes.White, path);
                } else {
                    g.FillPath(Brushes.Black, path);
                }
            }
        }
    }
}
