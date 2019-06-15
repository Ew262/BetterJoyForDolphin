﻿using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace BetterJoyForDolphin {
	public partial class MainForm : Form {
		public bool nonOriginal = Boolean.Parse(ConfigurationManager.AppSettings["NonOriginalController"]);
		public List<Button> con, loc, configBtn;
        public List<TextBox> ports;
		public bool calibrate;
		public List<KeyValuePair<string, float[]>> caliData;
		private Timer countDown;
		private int count;
		public List<int> xG, yG, zG, xA, yA, zA;

		public MainForm() {
            xG = new List<int>(); yG = new List<int>(); zG = new List<int>();
			xA = new List<int>(); yA = new List<int>(); zA = new List<int>();
			caliData = new List<KeyValuePair<string, float[]>> {
				new KeyValuePair<string, float[]>("0", new float[6] {0,0,0,-710,0,0})
			};

			InitializeComponent();

			if (!nonOriginal)
				AutoCalibrate.Hide();

			con = new List<Button> { con1, con2, con3, con4 };
			loc = new List<Button> { loc1, loc2, loc3, loc4 };
            configBtn = new List<Button> { config1, config2, config3, config4};

            //list all options
            string[] myConfigs = ConfigurationManager.AppSettings.AllKeys;
			Size childSize = new Size(87, 20);
			for (int i = 0; i != myConfigs.Length; i++) {
				tableLayoutPanel1.RowCount++;
				tableLayoutPanel1.Controls.Add(new Label() { Text = myConfigs[i], TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true, Size = childSize }, 0, i);

				var value = ConfigurationManager.AppSettings[myConfigs[i]];
				Control childControl;
				if (value == "true" || value == "false") {
					childControl = new CheckBox() { Checked = Boolean.Parse(value), Size = childSize };
				} else {
					childControl = new TextBox() { Text = value, Size = childSize };
				}

				tableLayoutPanel1.Controls.Add(childControl, 1, i);
				childControl.MouseClick += cbBox_Changed;
			}
		}

		private void HideToTray() {
			this.WindowState = FormWindowState.Minimized;
			notifyIcon.Visible = true;
			notifyIcon.ShowBalloonTip(1);
			this.ShowInTaskbar = false;
			this.Hide();
		}

		private void ShowFromTray() {
			this.Show();
			this.WindowState = FormWindowState.Normal;
			this.ShowInTaskbar = true;
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
			this.Icon = Properties.Resources.BetterJoyForDolphin_icon;
			notifyIcon.Visible = false;
		}

		private void MainForm_Resize(object sender, EventArgs e) {
			if (this.WindowState == FormWindowState.Minimized) {
				HideToTray();
			}
		}

		private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e) {
			ShowFromTray();
		}

		private void MainForm_Load(object sender, EventArgs e) {
			Config.Init(caliData);

			Program.Start();

			passiveScanBox.Checked = Config.Value("ProgressiveScan");
			startInTrayBox.Checked = Config.Value("StartInTray");

			if (Config.Value("StartInTray")) {
				HideToTray();
			} else {
				ShowFromTray();
			}
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
			try {
				Program.Stop();
				Environment.Exit(0);
			} catch { }
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e) { // this does not work, for some reason. Fix before release
			try {
				Program.Stop();
				Close();
				Environment.Exit(0);
			} catch { }
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			linkLabel1.LinkVisited = true;
			System.Diagnostics.Process.Start("http://paypal.me/DavidKhachaturov/5");
		}

		private void passiveScanBox_CheckedChanged(object sender, EventArgs e) {
			Config.Save("ProgressiveScan", passiveScanBox.Checked);
		}

		public void AppendTextBox(string value) { // https://stackoverflow.com/questions/519233/writing-to-a-textbox-from-another-thread
			if (InvokeRequired) {
				this.Invoke(new Action<string>(AppendTextBox), new object[] { value });
				return;
			}
			console.AppendText(value);
		}

		bool toRumble = Boolean.Parse(ConfigurationManager.AppSettings["EnableRumble"]);
		bool showAsXInput = Boolean.Parse(ConfigurationManager.AppSettings["ShowAsXInput"]);

        public void configBtnClick(object sender, EventArgs e, Joycon v)
        {
            if(v != null) { 
                Form df = new DolphinConfig(v);
                df.ShowDialog();
            }
        }

        public void locBtnClick(object sender, EventArgs e) {
			Button bb = sender as Button;

			if (bb.Tag.GetType() == typeof(Button)) {
				Button button = bb.Tag as Button;

				if (button.Tag.GetType() == typeof(Joycon)) {
					Joycon v = (Joycon)button.Tag;
					v.SetRumble(20.0f, 400.0f, 1.0f, 300);
				}
			}
		}

		public void conBtnClick(object sender, EventArgs e) {
			Button button = sender as Button;

			if (button.Tag.GetType() == typeof(Joycon)) {
				Joycon v = (Joycon)button.Tag;

				if (v.other == null && !v.isPro) { // needs connecting to other joycon (so messy omg)
					bool succ = false;
					foreach (Joycon jc in Program.mgr.j) {
						if (!jc.isPro && jc.isLeft != v.isLeft && jc != v && jc.other == null) {
							v.other = jc;
							jc.other = v;

                            if (jc.isLeft == true) {
                                jc.SetActiveOtherPack(false);
                            }
                            else {
                                v.SetActiveOtherPack(false);
                            }

							//Set both Joycon LEDs to the one with the lowest ID
							byte led = jc.LED <= v.LED ? jc.LED : v.LED;
							jc.LED = led;
							v.LED = led;
							jc.SetLED(led);
							v.SetLED(led);

							v.xin.Dispose();
							v.xin = null;

							foreach (Button b in con)
								if (b.Tag == jc)
									b.BackgroundImage = jc.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;

							succ = true;
							break;
						}
					}

					if (succ)
						foreach (Button b in con)
							if (b.Tag == v)
								b.BackgroundImage = v.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;
				} else if (v.other != null && !v.isPro) { // needs disconnecting from other joycon
					if (v.xin == null) {
						ReenableXinput(v);
						v.xin.Connect();
					}

					if (v.other.xin == null) {
						ReenableXinput(v.other);
						v.other.xin.Connect();
					}

					button.BackgroundImage = v.isLeft ? Properties.Resources.jc_left_s : Properties.Resources.jc_right_s;

					foreach (Button b in con)
						if (b.Tag == v.other)
							b.BackgroundImage = v.other.isLeft ? Properties.Resources.jc_left_s : Properties.Resources.jc_right_s;

					//Set original Joycon LEDs
					v.other.LED = (byte)(0x1 << v.other.PadId);
					v.LED = (byte)(0x1 << v.PadId);
					v.other.SetLED(v.other.LED);
					v.SetLED(v.LED);

                    v.other.SetActiveOtherPack(true);
                    v.other.other.SetActiveOtherPack(true);
                    v.other.other = null;
					v.other = null;
                    
				}
			}
		}

		private void startInTrayBox_CheckedChanged(object sender, EventArgs e) {
			Config.Save("StartInTray", startInTrayBox.Checked);
		}

		private void btn_open3rdP_Click(object sender, EventArgs e) {
			_3rdPartyControllers partyForm = new _3rdPartyControllers();
			partyForm.ShowDialog();
		}

		private void button1_Click(object sender, EventArgs e) {
			Application.Restart();
			Environment.Exit(0);
		}

		void ReenableXinput(Joycon v) {
			if (showAsXInput) {
				v.xin = new Xbox360Controller(Program.emClient);

				if (toRumble)
					v.xin.FeedbackReceived += v.ReceiveRumble;
				v.report = new Xbox360Report();
			}
		}

		private void label2_Click(object sender, EventArgs e) {
			rightPanel.Visible = !rightPanel.Visible;
			foldLbl.Text = rightPanel.Visible ? "<" : ">";
		}

        // JC CHANGED SETTINGS
		private void cbBox_Changed(object sender, EventArgs e) {
			var coord = tableLayoutPanel1.GetPositionFromControl(sender as Control);

			var valCtl = tableLayoutPanel1.GetControlFromPosition(coord.Column, coord.Row);
			var KeyCtl = tableLayoutPanel1.GetControlFromPosition(coord.Column - 1, coord.Row).Text;

			try {
				var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
				var settings = configFile.AppSettings.Settings;
				if (sender.GetType() == typeof(CheckBox) && settings[KeyCtl] != null) {
					settings[KeyCtl].Value = ((CheckBox)valCtl).Checked.ToString().ToLower();
				} else if (sender.GetType() == typeof(TextBox) && settings[KeyCtl] != null) {
					settings[KeyCtl].Value = ((TextBox)valCtl).Text.ToLower();
				}
				configFile.Save(ConfigurationSaveMode.Modified);
				ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
			} catch (ConfigurationErrorsException) {
				Console.WriteLine("Error writing app settings");
				Trace.WriteLine(String.Format("rw {0}, column {1}, {2}, {3}", coord.Row, coord.Column, sender.GetType(), KeyCtl));
			}
		}
		private void StartCalibrate(object sender, EventArgs e) {
			if (Program.mgr.j.Count == 0) {
				this.console.Text = "Please connect a single pro controller.";
				return;
			}
			if (Program.mgr.j.Count > 1) {
				this.console.Text = "Please calibrate one controller at a time (disconnect others).";
				return;
			}
			this.AutoCalibrate.Enabled = false;
			countDown = new Timer();
			this.count = 4;
			this.CountDown(null, null);
			countDown.Tick += new EventHandler(CountDown);
			countDown.Interval = 1000;
			countDown.Enabled = true;
		}

		private void StartGetData() {
			this.xG.Clear(); this.yG.Clear(); this.zG.Clear();
			this.xA.Clear(); this.yA.Clear(); this.zA.Clear();
			countDown = new Timer();
			this.count = 3;
			this.calibrate = true;
			countDown.Tick += new EventHandler(CalcData);
			countDown.Interval = 1000;
			countDown.Enabled = true;
		}

        private void version_lbl_Click(object sender, EventArgs e)
        {

        }

        private void con1_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void CountDown(object sender, EventArgs e) {
			if (this.count == 0) {
				this.console.Text = "Calibrating...";
				countDown.Stop();
				this.StartGetData();
			} else {
				this.console.Text = "Plese keep the controller flat." + "\r\n";
				this.console.Text += "Calibration will start in " + this.count + " seconds.";
				this.count--;
			}
		}
		private void CalcData(object sender, EventArgs e) {
			if (this.count == 0) {
				countDown.Stop();
				this.calibrate = false;
				string serNum = Program.mgr.j.First().serial_number;
				int serIndex = this.findSer(serNum);
				float[] Arr = new float[6] { 0, 0, 0, 0, 0, 0 };
				if (serIndex == -1) {
					this.caliData.Add(new KeyValuePair<string, float[]>(
						 serNum,
						 Arr
					));
				} else {
					Arr = this.caliData[serIndex].Value;
				}
				Random rnd = new Random();
				Arr[0] = (float)quickselect_median(this.xG, rnd.Next);
				Arr[1] = (float)quickselect_median(this.yG, rnd.Next);
				Arr[2] = (float)quickselect_median(this.zG, rnd.Next);
				Arr[3] = (float)quickselect_median(this.xA, rnd.Next);
				Arr[4] = (float)quickselect_median(this.yA, rnd.Next);
				Arr[5] = (float)quickselect_median(this.zA, rnd.Next) - 4010; //Joycon.cs acc_sen 16384
				this.console.Text += "Calibration completed!!!" + "\r\n";
				Config.SaveCaliData(this.caliData);
				Program.mgr.j.First().getActiveData();
				this.AutoCalibrate.Enabled = true;
			} else {
				this.count--;
			}

		}
		private double quickselect_median(List<int> l, Func<int, int> pivot_fn) {
			int ll = l.Count;
			if (ll % 2 == 1) {
				return this.quickselect(l, ll / 2, pivot_fn);
			} else {
				return 0.5 * (quickselect(l, ll / 2 - 1, pivot_fn) + quickselect(l, ll / 2, pivot_fn));
			}
		}

		private int quickselect(List<int> l, int k, Func<int, int> pivot_fn) {
			if (l.Count == 1 && k == 0) {
				return l[0];
			}
			int pivot = l[pivot_fn(l.Count)];
			List<int> lows = l.Where(x => x < pivot).ToList();
			List<int> highs = l.Where(x => x > pivot).ToList();
			List<int> pivots = l.Where(x => x == pivot).ToList();
			if (k < lows.Count) {
				return quickselect(lows, k, pivot_fn);
			} else if (k < (lows.Count + pivots.Count)) {
				return pivots[0];
			} else {
				return quickselect(highs, k - lows.Count - pivots.Count, pivot_fn);
			}
		}

		public float[] activeCaliData(string serNum) {
			for (int i = 0; i < this.caliData.Count; i++) {
				if (this.caliData[i].Key == serNum) {
					return this.caliData[i].Value;
				}
			}
			return this.caliData[0].Value;
		}

		private int findSer(string serNum) {
			for (int i = 0; i < this.caliData.Count; i++) {
				if (this.caliData[i].Key == serNum) {
					return i;
				}
			}
			return -1;
		}
	}
}
