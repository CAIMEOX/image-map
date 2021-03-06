﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Map
{
    public partial class ImportWindow : Form
    {
        private bool Finished = false;
        private int EditingIndex = 0;
        private string[] InputPaths;
        private Image CurrentImage;
        public List<Bitmap> OutputImages = new List<Bitmap>();
        RotateFlipType Rotation = RotateFlipType.RotateNoneFlipNone;
        public ImportWindow()
        {
            InitializeComponent();
            InterpolationModeBox.SelectedIndex = 0;
        }

        public void StartImports(Form parent, string[] inputpaths)
        {
            InputPaths = inputpaths;
            OutputImages.Clear();
            CurrentIndexLabel.Visible = (InputPaths.Length > 1);
            ApplyAllCheck.Visible = (InputPaths.Length > 1);
            EditingIndex = -1;
            ProcessNextImage();
            if (!Finished) // don't try to show if all loaded images were skipped
                ShowDialog(parent);
        }

        private void ProcessNextImage()
        {
            EditingIndex++;
            if (EditingIndex >= InputPaths.Length)
                Finish();
            else
            {
                string filename = Path.GetFileName(InputPaths[EditingIndex]);
                try
                {
                    CurrentImage = Image.FromFile(InputPaths[EditingIndex]);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"The image {filename} could not be loaded (probably not an image file)","Bad image!");
                    ProcessNextImage();
                    return;
                }
                Rotation = RotateFlipType.RotateNoneFlipNone;
                PreviewBox.Image = CurrentImage;
                this.Text = "Import – " + filename;
                PreviewBox.Interp = GetInterpolationMode();
                CurrentImage.RotateFlip(Rotation);
                CurrentIndexLabel.Text = $"{EditingIndex + 1} / {InputPaths.Length}";
            }
        }

        private void Finish()
        {
            Finished = true;
            InputPaths = null;
            this.Close();
        }

        private void DimensionsInput_ValueChanged(object sender, EventArgs e)
        {
            PreviewBox.Width = (int)((double)WidthInput.Value / (double)HeightInput.Value * PreviewBox.Height);
            PreviewBox.Left = this.Width / 2 - (PreviewBox.Width / 2);
        }

        private void PreviewBox_Paint(object sender, PaintEventArgs e)
        {
            Pen black = new Pen(Color.Black, 3f);
            Pen white = new Pen(Color.White, 1f);
            for (int i = 1; i < WidthInput.Value; i++)
            {
                int split = (int)((double)PreviewBox.Width / (double)WidthInput.Value * i);
                e.Graphics.DrawLine(black, new Point(split, 0), new Point(split, PreviewBox.Height));
                e.Graphics.DrawLine(white, new Point(split, 0), new Point(split, PreviewBox.Height));
            }
            for (int i = 1; i < HeightInput.Value; i++)
            {
                int split = (int)((double)PreviewBox.Height / (double)HeightInput.Value * i);
                e.Graphics.DrawLine(black, new Point(0, split), new Point(PreviewBox.Width, split));
                e.Graphics.DrawLine(white, new Point(0, split), new Point(PreviewBox.Width, split));
            }
        }

        private InterpolationMode GetInterpolationMode()
        {
            if (InterpolationModeBox.SelectedIndex == 1)
                return InterpolationMode.NearestNeighbor;
            else if (InterpolationModeBox.SelectedIndex == 2)
                return InterpolationMode.HighQualityBicubic;
            else // automatic
                return (CurrentImage.Height > 128 && CurrentImage.Width > 128) ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        }

        private void InterpolationModeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PreviewBox.Image == null)
                return;
            PreviewBox.Interp = GetInterpolationMode();
        }

        private static Bitmap CropImage(Image img, Rectangle cropArea)
        {
            Bitmap bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, PixelFormat.DontCare);
        }

        private static Bitmap ResizeImg(Image image, int width, int height, InterpolationMode mode)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = mode;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            int index = EditingIndex;
            int final = ApplyAllCheck.Checked ? InputPaths.Length - 1 : EditingIndex;
            for (int i = index; i <= final; i++)
            {
                if (i > index)
                {
                    CurrentImage = Image.FromFile(InputPaths[i]);
                    CurrentImage.RotateFlip(Rotation);
                }
                Bitmap img = ResizeImg(CurrentImage, (int)(128 * WidthInput.Value), (int)(128 * HeightInput.Value), GetInterpolationMode());
                for (int y = 0; y < HeightInput.Value; y++)
                {
                    for (int x = 0; x < WidthInput.Value; x++)
                    {
                        OutputImages.Add(CropImage(img, new Rectangle(
                            (int)(x * img.Width / WidthInput.Value),
                            (int)(y * img.Height / HeightInput.Value),
                            (int)(img.Width / WidthInput.Value),
                            (int)(img.Height / HeightInput.Value))));
                    }
                }
            }
            EditingIndex = final;
            ProcessNextImage();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (ApplyAllCheck.Checked)
                Finish();
            else
                ProcessNextImage();
        }

        private void RotateButton_Click(object sender, EventArgs e)
        {
            if (Rotation == RotateFlipType.RotateNoneFlipNone)
                Rotation = RotateFlipType.Rotate90FlipNone;
            else if (Rotation == RotateFlipType.Rotate90FlipNone)
                Rotation = RotateFlipType.Rotate180FlipNone;
            else if (Rotation == RotateFlipType.Rotate180FlipNone)
                Rotation = RotateFlipType.Rotate270FlipNone;
            else if (Rotation == RotateFlipType.Rotate270FlipNone)
                Rotation = RotateFlipType.RotateNoneFlipNone;
            CurrentImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
            PreviewBox.Refresh();
        }
    }
}
