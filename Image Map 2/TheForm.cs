﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;


namespace Image_Map
{
    public partial class TheForm : Form
    {
        MinecraftWorld SelectedWorld;

        string LastOpenPath = "";
        string LastExportPath = "";
        CommonOpenFileDialog SelectWorldDialog = new CommonOpenFileDialog()
        {
            Title = "Select a Minecraft world folder",
            IsFolderPicker = true,
        };
        CommonOpenFileDialog ExportDialog = new CommonOpenFileDialog()
        {
            Title = "Export your maps somewhere",
            IsFolderPicker = true,
        };
        OpenFileDialog OpenDialog = new OpenFileDialog()
        {
            Title = "Import image files to turn into maps",
            Filter = "Image Files|*.png;*.bmp;*.jpg;*.gif|All Files|*.*",
            Multiselect = true,
        };
        ImportWindow ImportDialog = new ImportWindow();
        List<MapIDControl> ImportingMaps = new List<MapIDControl>();
        List<MapIDControl> ExistingMaps = new List<MapIDControl>();
        public TheForm()
        {
            InitializeComponent();
        }

        private void TheForm_Load(object sender, EventArgs e)
        {
            // load up saved settings
            LastOpenPath = Properties.Settings.Default.LastOpenPath;
            LastExportPath = Properties.Settings.Default.LastExportPath;
            ImportDialog.InterpolationModeBox.SelectedIndex = Properties.Settings.Default.InterpIndex;
            ImportDialog.ApplyAllCheck.Checked = Properties.Settings.Default.ApplyAllCheck;
            AddChestCheck.Checked = Properties.Settings.Default.AddChest;
        }

        private void OpenWorld(string folder)
        {
            try
            {
                SelectedWorld = new JavaWorld(folder);
            }
            catch (Exception ex1)
            {
                try
                {
                    SelectedWorld = new BedrockWorld(folder);
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Encountered the following errors while loading this world:\n\n{ex1.Message}\n{ex2.Message}", "Ouchie ouch");
                    return;
                }
            }
            MapView.Visible = true;
            ImportingMaps.Clear();
            ImportZone.Controls.Clear();
            ExistingMaps.Clear();
            ExistingZone.Controls.Clear();
            foreach (var map in SelectedWorld.WorldMaps.OrderBy(x => x.Key))
            {
                MapIDControl mapbox = new MapIDControl(map.Key, map.Value);
                mapbox.MouseDown += ExistingBox_MouseDown;
                ExistingMaps.Add(mapbox);
                ExistingZone.Controls.Add(mapbox);
            }
            WorldNameLabel.Text = SelectedWorld.Name;
        }

        private void SelectWorldButton_Click(object sender, EventArgs e)
        {
            SelectWorldDialog.InitialDirectory = LastExportPath;
            if (SelectWorldDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (!ImportingMaps.Any() || MessageBox.Show("You have unsaved maps waiting to be imported! If you select a new world, these will be lost!\n\nDiscard unsaved maps?", "Wait a minute!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    LastExportPath = SelectWorldDialog.FileName;
                    OpenWorld(SelectWorldDialog.FileName);
                }
            }
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            OpenDialog.InitialDirectory = LastOpenPath;
            if (OpenDialog.ShowDialog() == DialogResult.OK)
            {
                LastOpenPath = Path.GetDirectoryName(OpenDialog.FileName);
                var images = OpenDialog.FileNames.Select(x => Image.FromFile(x));
                ImportDialog.StartImports(this, images.ToList());
                var taken = ImportingMaps.Select(x => x.ID).Concat(ExistingMaps.Select(x => x.ID)).ToList();
                taken.Add(-1);
                long id = taken.Max() + 1;
                foreach (var image in ImportDialog.OutputImages)
                {
                    MapIDControl mapbox = new MapIDControl(id, SelectedWorld is JavaWorld ? (Map)new JavaMap(image) : new BedrockMap(image));
                    ImportingMaps.Add(mapbox);
                    ImportZone.Controls.Add(mapbox);
                    mapbox.MouseDown += ImportingBox_MouseDown;
                    id++;
                }
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            SelectedWorld.AddMaps(ImportingMaps.ToDictionary(x => x.ID, x => x.Map));
            if (AddChestCheck.Checked)
                SelectedWorld.AddChests(ImportingMaps.Select(x => x.ID));
            ExistingMaps.AddRange(ImportingMaps);
            ExistingZone.Controls.AddRange(ImportingMaps.ToArray());
            ImportingMaps.Clear();
            ImportZone.Controls.Clear();
        }

        // right-click maps to remove them
        private void ImportingBox_MouseDown(object sender, MouseEventArgs e)
        {
            MapIDControl box = sender as MapIDControl;
            if (e.Button == MouseButtons.Right)
            {
                ImportingMaps.Remove(box);
                ImportZone.Controls.Remove(box);
            }
        }

        private void ExistingBox_MouseDown(object sender, MouseEventArgs e)
        {
            MapIDControl box = sender as MapIDControl;
            if (e.Button == MouseButtons.Right && MessageBox.Show("Deleting this map will remove all copies of it from the world permanently.\n\nWould you like to delete this map?", "Delete this map?", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                ExistingMaps.Remove(box);
                ExistingZone.Controls.Remove(box);
                SelectedWorld.RemoveMap(box.ID);
            }
        }

        private void TheForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // save settings
            Properties.Settings.Default.LastExportPath = LastExportPath;
            Properties.Settings.Default.LastOpenPath = LastOpenPath;
            Properties.Settings.Default.InterpIndex = ImportDialog.InterpolationModeBox.SelectedIndex;
            Properties.Settings.Default.ApplyAllCheck = ImportDialog.ApplyAllCheck.Checked;
            Properties.Settings.Default.AddChest = AddChestCheck.Checked;
            Properties.Settings.Default.Save();
        }
    }

    public enum Edition
    {
        Java,
        Bedrock
    }
}