using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace NumberProcessor_Global_2022
{
    public partial class FrmDialog : Form
    {
        private string outputPath;
        public FrmDialog(string outputPath, string report)
        {
            InitializeComponent();

            this.outputPath = outputPath;
            this.txtReport.Text = report;
            this.outputPath = outputPath;
            if (new FileInfo(outputPath).Length == 0)
            {
                lblSaveResult.Text = "No Matching Rows Found !";
                btnNo.Enabled = false;
                btnYes.Enabled = false;
                File.Delete(outputPath);
            }
        }

        private void FrmDialog_Load(object sender, EventArgs e)
        {
            //btnYes.Focus();
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            SaveFileDialog s = new SaveFileDialog();
            s.RestoreDirectory = true;
            s.CheckFileExists = false;
            s.CheckPathExists = false;
            s.CreatePrompt = false;
            s.OverwritePrompt = true;
            s.Title = "Please choose a filepath to save filtered result.";
            s.Filter = "Text File|*.txt";
            DialogResult ans = s.ShowDialog();
            if (ans == DialogResult.OK)
            {
                if (File.Exists(s.FileName))
                    File.Delete(s.FileName);
                File.Move(outputPath, s.FileName);
                this.Close();
            }
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            this.Close();
        }

        private void FrmDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}
