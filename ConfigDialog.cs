using System;
using System.Windows.Forms;

namespace SDRSharp.V4L2
{
	public class ConfigDialog : Form
	{
		private readonly LibV4LIO owner;
		private System.Windows.Forms.NumericUpDown NumericUpDownSampleRate;
		private System.Windows.Forms.Label labelSampleRate;
		
		public ConfigDialog (LibV4LIO owner)
		{
			this.owner = owner;
			
			this.labelSampleRate = new System.Windows.Forms.Label();
			this.labelSampleRate.Location = new System.Drawing.Point(0, 0);
			this.labelSampleRate.Text = "Sample Rate";
			this.Controls.Add(this.labelSampleRate);

			this.NumericUpDownSampleRate = new System.Windows.Forms.NumericUpDown();
			this.NumericUpDownSampleRate.Maximum = 20000000;
			this.NumericUpDownSampleRate.Minimum = 900000;
			this.NumericUpDownSampleRate.Value = (decimal) this.owner.Samplerate;
			this.NumericUpDownSampleRate.Location = new System.Drawing.Point(150, 0);
            this.NumericUpDownSampleRate.ValueChanged += new System.EventHandler(this.NumericUpDownSampleRate_ValueChanged);
			this.Controls.Add(this.NumericUpDownSampleRate);
			
			this.Text = "V4L2 Config";
			this.TopMost = true;
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.RTLTcpSettings_FormClosing);
		}
		
		private void RTLTcpSettings_FormClosing(object sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			this.owner.HideSettingGUI();
		}

		private void NumericUpDownSampleRate_ValueChanged(object sender, EventArgs e)
		{
			owner.Samplerate = (double) NumericUpDownSampleRate.Value;
		}
	}
}
