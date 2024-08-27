using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using System.Globalization;
using System.Timers;

namespace Test_Fluidics
{
    public partial class Form1 : Form
    {


        // Mode Table Filename
        static string filename = "CytovilleModeTable.xml";

        // USB Virtual Serial Port
        //static SerialPort sPort = new SerialPort();

        // Mode Table
        ModeTable cfmt;

        // Comms Listener
        private Thread readThread;

        // Variable Monitor
        private Thread monitorThread;

        // Serial Port Mutex
        private static Mutex serMut = new Mutex();

        private static bool monitorPoll = false;
        private static bool sensirionPoll = false;

        // Command
        private byte[] globmsg = new byte[1];
        private bool takes_args;

        delegate void SetTextCallback(string text, TextBox textBox);
        delegate void ChangeButtonColorCallback(Color color, Button button);
        private IFluidicsDevice _Device;
        Object _LockObject;
        private CancellationTokenSource _DisconnectCancellationTokenSource;
        private System.Timers.Timer _PollTimer;
        private static AsyncPump _AsyncPump;
        public Form1()
        {
            InitializeComponent();

            // Initialize Commands
            //ArrayList Commands = new ArrayList();

            //Commands.Add(new Command("getLaserDelay", Command.GET_LASER_DELAY, false));
            //Commands.Add(new Command("setShutter", Command.SET_SHUTTER, false));
            //Commands.Add(new Command("getVersion", Command.GET_VERSION, false));
            //Commands.Add(new Command("eStop", Command.E_STOP, false));
            //Commands.Add(new Command("setPress", Command.SET_PRESS, true));
            //Commands.Add(new Command("getPress", Command.GET_PRESS, false));
            //Commands.Add(new Command("getSample", Command.GET_SAMPLE, false));
            //Commands.Add(new Command("setPresTol", Command.SET_PRESS_TOL, true));
            //Commands.Add(new Command("getPresTol", Command.GET_PRESS_TOL, false));
            //Commands.Add(new Command("setRefTemp", Command.SET_REF_TEMP, true));
            //Commands.Add(new Command("getRefTemp", Command.GET_REF_TEMP, false));
            //Commands.Add(new Command("getTemp", Command.GET_TEMP, false));
            //Commands.Add(new Command("setTempFactor", Command.SET_TEMP_FACTOR, true));
            //Commands.Add(new Command("getTempFactor", Command.GET_TEMP_FACTOR, false));
            //Commands.Add(new Command("execMode", Command.EXEC_MODE, true));
            //Commands.Add(new Command("setCalValues", Command.SET_CAL_VALUES, true));
            //Commands.Add(new Command("getCalValues", Command.GET_CAL_VALUES, false));
            //Commands.Add(new Command("getSens", Command.GET_SENSOR, true));
            //Commands.Add(new Command("getValve", Command.GET_VALVE, true));
            //Commands.Add(new Command("getPump", Command.GET_PUMP, true));
            //Commands.Add(new Command("setValve", Command.SET_VALVE, true));
            //Commands.Add(new Command("setPump", Command.SET_PUMP, true));
            //Commands.Add(new Command("getLaser", Command.GET_LASER, true));
            //Commands.Add(new Command("setLaser", Command.SET_LASER, true));
            //Commands.Add(new Command("setLaserDelay", Command.SET_LASER_DELAY, true));
            //Commands.Add(new Command("increaseFlowRate", Command.INCREASE_FLOW_RATE, true));
            //Commands.Add(new Command("decreaseFlowRate", Command.DECREASE_FLOW_RATE, true));
            //Commands.Add(new Command("ReadSensirion", Command.READ_SENSIRION, false));
            //Commands.Add(new Command("setController", Command.SET_CONTROLLER, true));
            //Commands.Add(new Command("setPID", Command.SET_PID, true));
            //Commands.Add(new Command("getPID", Command.GET_PID, false));
            //Commands.Add(new Command("getSITtolerance", Command.GET_SIT_TOL, false));
            //Commands.Add(new Command("getSITposition", Command.GET_SIT_POS, false));
            //Commands.Add(new Command("setSITtolerance", Command.SET_SIT_TOL, false));
            //Commands.Add(new Command("calSIT", Command.CALIBRATE_SIT, false));
            //Commands.Add(new Command("moveSIT", Command.MOVE_SIT, false));
            //Commands.Add(new Command("homeSIT", Command.HOME_SIT, false));

            //Commands.Add(new Command("setTube", Command.SET_TUBE_K, true));
            //Commands.Add(new Command("getTube", Command.GET_TUBE_K, false));
            //Commands.Add(new Command("setNoTube", Command.SET_NOTUBE_K, true));
            //Commands.Add(new Command("getNoTube", Command.GET_NOTUBE_K, false));

            comboBoxCommand.DataSource = Command.Commands;

            comboBoxCommand.DisplayMember = "Msg";
            //comboBoxCommand.ValueMember = "Code";
            _LockObject = new Object();
            _DisconnectCancellationTokenSource = new CancellationTokenSource();
            _Device = null;
            AllocationCommandProcessingBlocks();
            _FlowRateCalibrationInfos = new List<FlowRateCalibrationInfo>()
            {
            new FlowRateCalibrationInfo {Name="Low",Target=15,Spec=2,ModelTableIndex=2},
            new FlowRateCalibrationInfo {Name="Med",Target=30,Spec=3,ModelTableIndex=1},
            new FlowRateCalibrationInfo {Name="High",Target=60,Spec=6,ModelTableIndex=0},
            new FlowRateCalibrationInfo {Name="LoaderHigh",Target=100,Spec=10,ModelTableIndex=0}
            };
            _CancellationTokenSource = new CancellationTokenSource();
            //_PollTimer = new System.Timers.Timer(330);
            //_PollTimer.Elapsed += FluidicsPollTimer_Elapsed;
            _AsyncPump = new AsyncPump();
        }
        private void FluidicsPollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ReadSensirion();
        }
        private List<FlowRateCalibrationInfo> _FlowRateCalibrationInfos;
        private CancellationTokenSource _CancellationTokenSource;

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void WriteModeTable()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ModeTable));

            try
            {
                using (var fs = new FileStream(filename, FileMode.Create))
                {
                    serializer.Serialize(fs, cfmt);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "FLD Test APP ERROR!!!");
            }
        }

        private void ReadModeTable(string filename)
        {
            // Deserialize from XML file into Class type
            XmlSerializer serializer = new XmlSerializer(typeof(ModeTable));

            try
            {
                FileStream fs = new FileStream(filename, FileMode.Open);

                // Use the Deserialize method to restore the object's state with
                // data from the XML document. 
                cfmt = (ModeTable)serializer.Deserialize(fs);

                fs.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "FLD Test APP ERROR!!!");
            }
        }
        private FluidicsVersion _SampleFlowChangedVersion = new FluidicsVersion("0.9.74");
        public bool CheckSampleFlowType(string fluidicsVersion)
        {
            if (string.IsNullOrEmpty(fluidicsVersion))
            {
                return false;
            }

            FluidicsVersion newFluidicsVersion = new FluidicsVersion(fluidicsVersion);
            if (newFluidicsVersion >= _SampleFlowChangedVersion)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private string _FluidicsVersion = string.Empty;
        private void buttonloadmt_Click(object sender, EventArgs e)
        {
            _FluidicsVersion = GetFirmwareVersion();
            //if (sPort.IsOpen != true) return;

            // Load Mode Table from Disk 
            ReadModeTable(filename);

            listBoxModes.Items.Clear();

            if (cfmt != null)
            {
                foreach (CytovilleFluidicsMode mode in cfmt.Mode)
                {
                    // Populate listBox
                    listBoxModes.Items.Add(mode.ModeName);
                }


                // Send Mode Table to FLD 
                serMut.WaitOne();
                byte[] msg = new byte[1];
                msg[0] = Command.LOAD_MODE_TABLE;
                _Device.WriteBytesOff(msg, 0, 1);

                // Timing Table first
                float[] buf1 = new float[20];
                buf1[0] = cfmt.TimingTable.KT01;
                buf1[1] = cfmt.TimingTable.KT02;
                buf1[2] = cfmt.TimingTable.KT03;
                buf1[3] = cfmt.TimingTable.KT04;
                buf1[4] = cfmt.TimingTable.KT05;
                buf1[5] = cfmt.TimingTable.KT06;
                buf1[6] = cfmt.TimingTable.KT07;
                buf1[7] = cfmt.TimingTable.KT08;
                buf1[8] = cfmt.TimingTable.KT09;
                buf1[9] = cfmt.TimingTable.KT10;
                buf1[10] = cfmt.TimingTable.KT11;
                buf1[11] = cfmt.TimingTable.KT12;
                buf1[12] = cfmt.TimingTable.KT13;
                buf1[13] = cfmt.TimingTable.KT14;
                buf1[14] = cfmt.TimingTable.KT15;
                buf1[15] = cfmt.TimingTable.KT16;
                buf1[16] = cfmt.TimingTable.KT17;
                buf1[17] = cfmt.TimingTable.KT18;
                buf1[18] = cfmt.TimingTable.KT19;
                buf1[19] = cfmt.TimingTable.KT20;

                // create a byte array and copy the floats into it...
                var buffer = new byte[buf1.Length * sizeof(float)];
                Buffer.BlockCopy(buf1, 0, buffer, 0, buffer.Length);
                _Device.WriteBytesOff(buffer, 0, buffer.Length);

                // Calibration Table
                // Pressure DIFF
                buf1[0] = cfmt.CalibrationTable.Pressure.Diff;
                Buffer.BlockCopy(buf1, 0, buffer, 0, sizeof(float));
                _Device.WriteBytesOff(buffer, 0, sizeof(float));

                // SampleFlow
                if (CheckSampleFlowType(_FluidicsVersion))
                {
                    float[] sampleFlowBuf = new float[11];
                    sampleFlowBuf[0] = (float)cfmt.CalibrationTable.SampleFlow.Low;
                    sampleFlowBuf[1] = (float)cfmt.CalibrationTable.SampleFlow.Med;
                    sampleFlowBuf[2] = (float)cfmt.CalibrationTable.SampleFlow.High;
                    sampleFlowBuf[3] = (float)cfmt.CalibrationTable.SampleFlow.Bst;
                    sampleFlowBuf[4] = (float)cfmt.CalibrationTable.SampleFlow.Fsh;
                    sampleFlowBuf[5] = (float)cfmt.CalibrationTable.SampleFlow.Mix;
                    Buffer.BlockCopy(sampleFlowBuf, 0, buffer, 0, 6 * sizeof(float));
                    _Device.WriteBytesOff(buffer, 0, 6 * sizeof(float));
                }
                else
                {
                    UInt32[] sampleFlowBuf = new UInt32[11];
                    sampleFlowBuf[0] = (UInt32)cfmt.CalibrationTable.SampleFlow.Low;
                    sampleFlowBuf[1] = (UInt32)cfmt.CalibrationTable.SampleFlow.Med;
                    sampleFlowBuf[2] = (UInt32)cfmt.CalibrationTable.SampleFlow.High;
                    sampleFlowBuf[3] = (UInt32)cfmt.CalibrationTable.SampleFlow.Bst;
                    sampleFlowBuf[4] = (UInt32)cfmt.CalibrationTable.SampleFlow.Fsh;
                    sampleFlowBuf[5] = (UInt32)cfmt.CalibrationTable.SampleFlow.Mix;
                    Buffer.BlockCopy(sampleFlowBuf, 0, buffer, 0, 6 * sizeof(UInt32));
                    _Device.WriteBytesOff(buffer, 0, 6 * sizeof(UInt32));
                }


                // SIT
                UInt32[] buf2 = new UInt32[11];
                buf2[0] = (UInt32)cfmt.CalibrationTable.SIT.SITU;
                buf2[1] = (UInt32)cfmt.CalibrationTable.SIT.SITD;
                buf2[2] = (UInt32)cfmt.CalibrationTable.SIT.SITP;
                Buffer.BlockCopy(buf2, 0, buffer, 0, 3 * sizeof(UInt32));
                _Device.WriteBytesOff(buffer, 0, 3 * sizeof(UInt32));

                int modenum = 1;

                // Send Down Modes
                foreach (CytovilleFluidicsMode mode in cfmt.Mode)
                {
                    foreach (CytovilleFluidicsModeTimeSegment ts in mode.TimeSegment)
                    {
                        byte[] b = new byte[10];

                        // Send Valves
                        b[0] = (byte)ts.Valve.V1;
                        b[1] = (byte)ts.Valve.V2;
                        b[2] = (byte)ts.Valve.V3;
                        b[3] = (byte)ts.Valve.V4;
                        b[4] = (byte)ts.Valve.V5;
                        b[5] = (byte)ts.Valve.V6;
                        _Device.WriteBytesOff(b, 0, 6);

                        // Send Pumps
                        b[0] = (byte)ts.Pump.VACUUM;
                        b[1] = (byte)ts.Pump.P1;
                        b[2] = (byte)ts.Pump.P2;
                        b[3] = (byte)ts.Pump.P3;
                        _Device.WriteBytesOff(b, 0, 4);

                        // Send Sample Flowrate
                        b[0] = (byte)ts.Sample.FlowRate;
                        _Device.WriteBytesOff(b, 0, 1);

                        // Send Time Last

                        switch (ts.Time)
                        {
                            case "SITU": b[0] = 21; break;
                            case "SITD": b[0] = 22; break;
                            case "SITP": b[0] = 23; break;
                            case "TUBE": b[0] = 24; break;
                            case "NO_TUBE": b[0] = 25; break;
                            case "TOGG": b[0] = 26; break;

                            default:
                                // It's a KTxx Entry (1 to 20)
                                b[0] = Byte.Parse(ts.Time.Remove(0, 2));
                                break;
                        }

                        if ((b[0] == 26) && (cfmt.Mode.Length == modenum))
                        {
                            // Send an extra termination byte
                            b[1] = 0xAB;
                            _Device.WriteBytesOff(b, 0, 2);
                        }
                        else
                        {
                            _Device.WriteBytesOff(b, 0, 1);
                        }
                    }
                    modenum++;
                }
                serMut.ReleaseMutex();
            }

            textBoxFsh.Text = cfmt.CalibrationTable.SampleFlow.Fsh.ToString();
            textBoxBst.Text = cfmt.CalibrationTable.SampleFlow.Bst.ToString();
            textBoxHigh.Text = cfmt.CalibrationTable.SampleFlow.High.ToString();
            textBoxLow.Text = cfmt.CalibrationTable.SampleFlow.Low.ToString();
            textBoxMedium.Text = cfmt.CalibrationTable.SampleFlow.Med.ToString();
            textBoxSheath.Text = cfmt.CalibrationTable.Pressure.Diff.ToString();
            textBoxMix.Text = cfmt.CalibrationTable.SampleFlow.Mix.ToString();

            // GetsensirionCommand();
        }



        private void listBoxModes_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetText(listBoxModes.SelectedIndex.ToString(), textBoxModelIndex);
            SetText(0.ToString(), textBoxSendCount);
        }

        public void TargetMonitor()
        {
            while (true)
            {
                while (monitorPoll == true)
                {
                    try
                    {
                        // Read Temperature
                        if (monitorPoll) SendMessageWithoutArg(Command.GET_TEMP, 5000);
                        // Get Vaccum Pressure
                        if (monitorPoll) SendMessageWithoutArg(Command.GET_PRESS, 5000);
                        // Read Sensirion Sensor
                        if (monitorPoll || _SensirionOn == true) SendMessageWithoutArg(Command.READ_SENSIRION, 5000);
                        // Get Sample Pressure
                        if (monitorPoll) SendMessageWithoutArg(Command.GET_SAMPLE, 5000);
                        // Get Valves
                        for (int n = 1; n < 7; n++)
                        {
                            if (monitorPoll) SendMessageWithArg(Command.GET_VALVE, new float[] { n }, 5000);
                        }
                        // Get Pumps
                        for (int n = 1; n < 4; n++)
                        {
                            if (monitorPoll) SendMessageWithArg(Command.GET_PUMP, new float[] { n }, 5000);
                        }
                        // Get Sensors
                        for (int n = 1; n < 21; n++)
                        {
                            if (monitorPoll) SendMessageWithArg(Command.GET_SENSOR, new float[] { n }, 5000);
                        }
                        Thread.Sleep(100);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.ToString());
                    }
                }
                Thread.Sleep(100);
            }
        }



        private void SetText(string text, TextBox textBox)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (textBox.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.BeginInvoke(d, new object[] { text, textBox });
            }
            else
            {
                textBox.Text = text;
            }
        }

        private void ChangeButtonColor(Color color, Button button)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (button.InvokeRequired)
            {
                ChangeButtonColorCallback d = new ChangeButtonColorCallback(ChangeButtonColor);
                this.Invoke(d, new object[] { color, button });
            }
            else
            {
                button.ForeColor = color;
            }
        }

        private void comboBoxCOMPorts_MouseClick(object sender, MouseEventArgs e)
        {
            comboBoxCOMPorts.Items.Clear();
            comboBoxCOMPorts.Items.AddRange(SerialPort.GetPortNames());
        }

        private void comboBoxCOMPorts_SelectedIndexChanged(object sender, EventArgs e)
        {
            var portName = comboBoxCOMPorts.Text;
            if (string.IsNullOrEmpty(portName)) return;
            try
            {
                if (_Device != null)
                {
                    _Device.PortMessageReceived -= Device_PortMessageReceived;
                    _Device.Dispose();
                    _Device = null;
                }
                _Device = new SerialPortFluidicsDevice(portName); // HidFluidicsDevice.Enumerate().FirstOrDefault(); //
                _Device.PortMessageReceived += Device_PortMessageReceived;

                //sPort.Open();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "FLD Test App ERROR!!!");
            }

            if (_Device.IsOpen())
            {
                labelCOMPorts.Text = "CONNECTED";
                labelCOMPorts.Font = new Font(labelCOMPorts.Font, FontStyle.Bold);
                labelCOMPorts.ForeColor = Color.Green;

                //// Start comms listener
                //readThread = new Thread(ReadSerialPort);
                //readThread.IsBackground = true;
                //readThread.Start();

                // Start monitoring
                monitorThread = new Thread(TargetMonitor);
                monitorThread.IsBackground = true;
                monitorThread.Start();
            }
        }
        private void buttonDummy_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            ExecuteAsync(new ByteCodeWithoutArg(Command.DUMMY_MESSAGE));
        }

        private void groupBoxFLDCommands_Enter(object sender, EventArgs e)
        {

        }

        private void buttonSendCmd_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            PollingOff();

            char[] delimiterChars = { ' ' };

            string[] args = comboBoxCommand.Text.Split(delimiterChars);

            // Send Message to the FLD Board
            int n = 0;
            float[] arg = new float[args.Length - 1];
            if ((args.Length == 1) && takes_args)
            {
                MessageBox.Show("Command takes arguments", "FLD Test App");
                return;
            }

            try
            {
                foreach (string s in args)
                {
                    if (n == 0)
                    {
                        n++;
                        continue;
                    }
                    // create a byte array and copy the floats into it...
                    arg[n - 1] = float.Parse(s, CultureInfo.InvariantCulture);
                    n++;
                }
                ExecuteAsync(new ByteCodeWithArgs(Command.FromCode(globmsg[0]).Code, arg));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            // serMut.WaitOne();

            //if ((args.Length == 1) && takes_args)
            //{
            //    MessageBox.Show("Command takes arguments", "FLD Test App");
            //    return;
            //}
            //sPort.Write(globmsg, 0, 1);

            //int n = 0;
            //float[] arg = new float[10];
            //foreach (string s in args)
            //{
            //    if (n == 0)
            //    {
            //        n++;
            //        continue;
            //    }
            //    // create a byte array and copy the floats into it...
            //    arg[n - 1] = float.Parse(s);
            //    n++;
            //}
            //var buffer = new byte[(n - 1) * sizeof(float)];
            //Buffer.BlockCopy(arg, 0, buffer, 0, buffer.Length);
            //sPort.Write(buffer, 0, buffer.Length);

            //serMut.ReleaseMutex();
        }

        private void comboBoxCommand_SelectedIndexChanged(object sender, EventArgs e)
        {
            globmsg[0] = ((Command)(comboBoxCommand.SelectedValue)).Code;
            takes_args = ((Command)(comboBoxCommand.SelectedValue)).Args;
        }

        private void buttonPoll_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;


            if (buttonPoll.ForeColor == Color.Red)
            {
                buttonPoll.ForeColor = Color.Green;
                monitorPoll = true;
            }
            else
            {
                buttonPoll.ForeColor = Color.Red;
                monitorPoll = false;
                sensirionPoll = false;
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void comboBoxCommand_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void comboBoxCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                buttonSendCmd_Click(this, null);
                e.SuppressKeyPress = true;
            }
        }

        private void buttonExecMode_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            var index = listBoxModes.SelectedIndex;
            if (index == -1) return;
            PollingOff();
            if (index == 18)
            {
                _CancellationTokenSource.Cancel();
                _SensirionOn = false;
                //_PollTimer.Stop();
            }
            SendModelTable(index);
            buttonExecMode.ForeColor = Color.Red;
        }

        private void buttonEstop_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true)
            {
                MessageBox.Show("Not connected to FLD board", "ERROR!!!");
                return;
            }
            PollingOff();
            ExecuteAsync(new ByteCodeWithoutArg(Command.E_STOP));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_Device != null)
            {
                _Device.PortMessageReceived -= Device_PortMessageReceived;
                _Device.Dispose();
                _Device = null;
            }
        }

        private void buttonIncreaseFlowRate_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            PollingOff();
            ExecuteAsync(new ByteCodeWithArgs(Command.DECREASE_FLOW_RATE, new float[] { 1 }));
        }

        private void buttonDecreaseFlowRate_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            PollingOff();
            ExecuteAsync(new ByteCodeWithArgs(Command.INCREASE_FLOW_RATE, new float[] { 1 }));
        }

        private void buttonSetFlowRate_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            SetFlowRate(float.Parse(textBoxSetFlowRate.Text));
        }

        private void buttonSensEnable_Click(object sender, EventArgs e)
        {
            if (sensirionPoll == true)
            {
                sensirionPoll = false;
            }
            else
            {
                sensirionPoll = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string sheath = textBoxSheath.Text;
            string high = textBoxHigh.Text;
            string low = textBoxLow.Text;
            string medium = textBoxMedium.Text;
            string bst = textBoxBst.Text;
            string fsh = textBoxFsh.Text;
            string mix = textBoxMix.Text;

            if (string.IsNullOrEmpty(sheath) || string.IsNullOrEmpty(high) || string.IsNullOrEmpty(low) || string.IsNullOrEmpty(medium))
            {
                MessageBox.Show("Please fill up all values!");
            }

            if (cfmt != null)
            {
                cfmt.CalibrationTable.Pressure.Diff = float.Parse(sheath);
                cfmt.CalibrationTable.SampleFlow.High = float.Parse(high);
                cfmt.CalibrationTable.SampleFlow.Med = float.Parse(medium);
                cfmt.CalibrationTable.SampleFlow.Low = float.Parse(low);
                cfmt.CalibrationTable.SampleFlow.Bst = float.Parse(bst);
                cfmt.CalibrationTable.SampleFlow.Fsh = float.Parse(fsh);
                cfmt.CalibrationTable.SampleFlow.Mix = float.Parse(mix);

                WriteModeTable();
            }
            else
            {
                MessageBox.Show("This operation requires mode table to be loaded!");
            }
        }

        private void textBoxSheath_TextChanged(object sender, EventArgs e)
        {
            textBoxSheath.BackColor = Color.White;
        }

        private void textBoxHigh_TextChanged(object sender, EventArgs e)
        {
            textBoxHigh.BackColor = Color.White;
        }

        private void textBoxMedium_TextChanged(object sender, EventArgs e)
        {
            textBoxMedium.BackColor = Color.White;
        }

        private void textBoxLow_TextChanged(object sender, EventArgs e)
        {
            textBoxLow.BackColor = Color.White;
        }

        private void textBoxBst_TextChanged(object sender, EventArgs e)
        {
            textBoxBst.BackColor = Color.White;
        }

        private void textBoxFsh_TextChanged(object sender, EventArgs e)
        {
            textBoxFsh.BackColor = Color.White;
        }

        private void textBoxMix_TextChanged(object sender, EventArgs e)
        {
            textBoxMix.BackColor = Color.White;
        }
        private void PollingOff()
        {
            buttonPoll.ForeColor = Color.Red;
            monitorPoll = false;
        }
        private float CalculateSlope(float initialFlowRate, float initialSampleFlowSetValue, float secondFlowRate, float secondSampleFlowSetValue)
        {
            if (secondSampleFlowSetValue == initialSampleFlowSetValue)
            {
                return 1;
            }
            return (secondFlowRate - initialFlowRate) / (secondSampleFlowSetValue - initialSampleFlowSetValue);
        }

        private float CalculateChangValue(float actualValue, float setFlowRate, float target, float slope)
        {
            var changeValue = setFlowRate - ((actualValue - target) / slope);

            return (float)Math.Round(changeValue);
        }
        
        private const int FLUIDICS_STABLIZATION_WAITING_TIME = 8000;
        private void AutoRun_Click(object sender, EventArgs e)
        {
            if (_Device.IsOpen() != true) return;
            if (listBoxModes.Items.Count == 0)
            {
                MessageBox.Show("You need load mode table", "Auto Calibration Error", MessageBoxButtons.OK);
                return;
            }
            _CancellationTokenSource = new CancellationTokenSource();
            try
            {
                Task.Run(() =>
                {
                    SendCalSIT();
                    if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                    var isFirst = true;
                    var slope = 0f;
                    //Message
                    var flowRateNameAndResult = new Dictionary<string, float>();
                    foreach (var flowRateCalibrationInfo in _FlowRateCalibrationInfos)
                    {
                        if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                        //Message
                        SendModelTable(flowRateCalibrationInfo.ModelTableIndex);
                        if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                        _SensirionOn = true;
                        System.Threading.Thread.Sleep(FLUIDICS_STABLIZATION_WAITING_TIME);
                        var sampleFlowSetValue = 0f;
                        switch (flowRateCalibrationInfo.Name)
                        {
                            case "Low":
                                sampleFlowSetValue = float.Parse(textBoxLow.Text);
                                break;
                            case "Med":
                                sampleFlowSetValue = float.Parse(textBoxMedium.Text);
                                break;
                            case "High":
                            case "LoaderHigh":
                                sampleFlowSetValue = float.Parse(textBoxHigh.Text);
                                break;
                        }
                        var currentFlowRate = _FlowRate;
                        if (isFirst)
                        {
                            var initialSampleFlowSetValue = 100;
                            var secondSampleFlowSetValue = 150;
                            SetFlowRate(initialSampleFlowSetValue);
                            if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                            System.Threading.Thread.Sleep(FLUIDICS_STABLIZATION_WAITING_TIME);
                            var initialFlowRate = _FlowRate;
                            SetFlowRate(secondSampleFlowSetValue);
                            if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                            System.Threading.Thread.Sleep(FLUIDICS_STABLIZATION_WAITING_TIME);
                            var secondFlowRate = _FlowRate;
                            slope = CalculateSlope(initialFlowRate, initialSampleFlowSetValue, secondFlowRate, secondSampleFlowSetValue);
                            SetFlowRate(sampleFlowSetValue);
                            if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                            System.Threading.Thread.Sleep(FLUIDICS_STABLIZATION_WAITING_TIME);
                            currentFlowRate = _FlowRate;
                            isFirst = false;
                        }
                        var checkResult = IsFlowRateMatchTarget(currentFlowRate, sampleFlowSetValue, flowRateCalibrationInfo.Target, flowRateCalibrationInfo.Spec, slope);
                        int changeTimes = 0;
                        while (checkResult.Item1 == false)
                        {
                            SetFlowRate(checkResult.Item3);
                            if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                            sampleFlowSetValue = checkResult.Item3;
                            System.Threading.Thread.Sleep(FLUIDICS_STABLIZATION_WAITING_TIME);
                            currentFlowRate = _FlowRate;
                            checkResult = IsFlowRateMatchTarget(currentFlowRate, sampleFlowSetValue, flowRateCalibrationInfo.Target, flowRateCalibrationInfo.Spec, slope);
                            changeTimes++;
                            if (changeTimes > 10 || checkResult.Item2 != 0)
                            {
                                _SensirionOn = false;
                                SendModelTable(18);
                                MessageBox.Show($"Failed to set flow rate. \nCalibration Flow Rate Name:{flowRateCalibrationInfo.Name}\nActual Flow Rate: {currentFlowRate} μL/min\nCurrent Set Piston Pump Position: {checkResult.Item3}", "APP Test", MessageBoxButtons.OK);
                                return;
                            }
                        }
                        flowRateNameAndResult.Add(flowRateCalibrationInfo.Name, sampleFlowSetValue);
                    }
                    _SensirionOn = false;
                    SendModelTable(18);
                    if (_CancellationTokenSource?.IsCancellationRequested == true) return;
                    MessageBox.Show($"Low :{flowRateNameAndResult["Low"]}     Medium :{flowRateNameAndResult["Med"]}     High :{flowRateNameAndResult["High"]}     LoaderHigh:{flowRateNameAndResult["LoaderHigh"]}", "APP Test", MessageBoxButtons.OK);
                }, _CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                try
                {
                    _SensirionOn = false;
                    SendModelTable(18);
                    MessageBox.Show(ex.Message, "Auto Calibration Result", MessageBoxButtons.OK);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
                    throw;
                }

            }


        }
        public string GetFirmwareVersion()
        {
            return _AsyncPump.Run(() => GetFirmwareVersionAsync(), 5000);
        }
        public string SendCalSIT()
        {
            return _AsyncPump.Run(() => SendCalSITAsync(), 600000);
        }
        public string SendModelTable(int modelTableIndex)
        {
            return _AsyncPump.Run(() => SendModelTableAsync(modelTableIndex), 600000);
        }
        public string SetFlowRate(float percentOfTravel)
        {
            return _AsyncPump.Run(() => SetFlowRateAsync(percentOfTravel), 600000);
        }
        private bool _SensirionOn = false;
        public string ReadSensirion()
        {
            if (_Device.IsOpen() == false || _SensirionOn == false) return null;
            return SendMessageWithoutArg(Command.READ_SENSIRION, 5000);
        }
        public string SendMessageWithoutArg(byte code, int timeoutMillis)
        {
            return _AsyncPump.Run(() => SendMessageWithoutArgAsync(code), timeoutMillis);
        }
        public string SendMessageWithArg(byte code, float[] arg, int timeoutMillis)
        {
            return _AsyncPump.Run(() => SendMessageWithArgAsync(code, arg), timeoutMillis);
        }
        private Task<string> GetFirmwareVersionAsync()
        {
            return ExecuteAsync(new ByteCodeWithoutArg(Command.GET_VERSION)).ContinueWith<string>((t) =>
            {
                var header = Command.FromCode(Command.GET_VERSION);
                return t.Result.Substring(12);
            }, TaskScheduler.Default);
        }
        private Task<string> SetFlowRateAsync(float percentOfTravel)
        {
            return ExecuteAsync(new ByteCodeWithArgs(Command.SET_FLOW_RATE, new float[] { percentOfTravel }));
        }
        private Task<string> SendCalSITAsync()
        {
            return ExecuteAsync(new ByteCodeWithArgs(Command.CALIBRATE_SIT, new float[0]));
        }
        private Task<string> SendModelTableAsync(int modelTableIndex)
        {
            return ExecuteAsync(new FluidicsMode(modelTableIndex));
        }
        private Task<string> SendMessageWithoutArgAsync(byte code)
        {
            return ExecuteAsync(new ByteCodeWithoutArg(code));
        }
        public Task<string> SendMessageWithArgAsync(byte code, float[] arg)
        {
            return ExecuteAsync(new ByteCodeWithArgs(code, arg));
        }


        private (bool, int, float) IsFlowRateMatchTarget(float actualValue, float setFlowRate, float target, float spec, float slope)
        {
            float FLOW_RATE_LOW_LIMIT = CheckSampleFlowType(_FluidicsVersion) ? -100 : 0;
            float FLOW_RATE_UP_LIMIT = 1000;
            int errorType = 0;
            float changeValue = 0;
            bool isFlowrateMatched = false;
            if (setFlowRate == 0)
            {
                setFlowRate = 1;
            }
            if (float.IsNaN(actualValue) == false)
            {
                if (float.IsNaN(target) == false && float.IsNaN(spec) == false)
                {
                    if (target - spec <= actualValue && actualValue <= target + spec)
                    {
                        isFlowrateMatched = true;
                    }
                    else
                    {
                        if (slope != 0)
                        {
                            if (actualValue < target - spec)
                            {
                                changeValue = setFlowRate + ((target - actualValue) / slope);
                            }
                            else if (actualValue > target + spec)
                            {
                                changeValue = setFlowRate - ((actualValue - target) / slope);
                            }
                            //changeValue = setFlowRate - ((actualValue - target) / slope);
                        }
                    }
                }
            }
            else
            {
                changeValue = setFlowRate;
            }
            if (isFlowrateMatched == false && (changeValue < FLOW_RATE_LOW_LIMIT || changeValue > FLOW_RATE_UP_LIMIT))
            {
                errorType = 1;
            }
            return (isFlowrateMatched, errorType, (float)Math.Round(changeValue));
        }
        private void GetsensirionCommand()
        {
            _PollTimer.Start();
        }

        private BlockingCollection<string> _PortReturnMessage;
        private TransformBlock<FluidicsCommand, bool> _FluidicsCommandWaiterTransformBlock;
        private TransformBlock<FluidicsCommand, FluidicsCommand> _FluidicsCommandExecutionTransformBlock;

        private float _FlowRate = 0f;
        private void Device_PortMessageReceived(object sender, FluidicsPortMessageEventArgs e)
        {
            if (e.Message.Contains("ModeTable r") == false && string.IsNullOrEmpty(e.Message) == false)
            {
                lock (_LockObject)
                {
                    if (_PortReturnMessage != null) _PortReturnMessage.Add(e.Message);
                }
            }
            try
            {
                string message = e.Message;
                char[] delimiterChars = { ' ' };
                if (message.Contains("getTemp:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    SetText(arguments[1], textBoxTemp);
                }
                else if (message.Contains("execModeEnd:"))
                {
                    SetText(message, textBoxFLDMsg);
                    ChangeButtonColor(Color.Black, buttonExecMode);
                }
                else if (message.Contains("getPres:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    SetText(arguments[1], textBoxVac);
                }
                else if (message.Contains("getFlowRate:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    SetText(arguments[1], textBoxSetFlowRate);
                }
                else if (message.Contains("readSensirion:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    _FlowRate = float.Parse(arguments[1]);
                    SetText(arguments[1], textBoxSensirion);
                    Console.WriteLine(arguments[1]);
                }
                else if (message.Contains("getSample:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    SetText(arguments[1], textBoxSample);
                }
                else if (message.Contains("getSensor:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    switch (arguments[1])
                    {
                        case "1":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS1);
                                }
                                else
                                {
                                    SetText("0", textBoxS1);
                                }
                            }
                            break;
                        case "2":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS2);
                                }
                                else
                                {
                                    SetText("0", textBoxS2);
                                }
                            }
                            break;
                        case "3":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS3);
                                }
                                else
                                {
                                    SetText("0", textBoxS3);
                                }
                            }
                            break;
                        case "4":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS4);
                                }
                                else
                                {
                                    SetText("0", textBoxS4);
                                }
                            }
                            break;
                        case "5":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS5);
                                }
                                else
                                {
                                    SetText("0", textBoxS5);
                                }
                            }
                            break;
                        case "6":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS6);
                                }
                                else
                                {
                                    SetText("0", textBoxS6);
                                }
                            }
                            break;
                        case "7":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS7);
                                }
                                else
                                {
                                    SetText("0", textBoxS7);
                                }
                            }
                            break;
                        case "8":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS8);
                                }
                                else
                                {
                                    SetText("0", textBoxS8);
                                }
                            }
                            break;
                        case "9":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS9);
                                }
                                else
                                {
                                    SetText("0", textBoxS9);
                                }
                            }
                            break;
                        case "10":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS10);
                                }
                                else
                                {
                                    SetText("0", textBoxS10);
                                }
                            }
                            break;
                        case "11":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS11);
                                }
                                else
                                {
                                    SetText("0", textBoxS11);
                                }
                            }
                            break;
                        case "12":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS12);
                                }
                                else
                                {
                                    SetText("0", textBoxS12);
                                }
                            }
                            break;
                        case "13":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS13);
                                }
                                else
                                {
                                    SetText("0", textBoxS13);
                                }
                            }
                            break;
                        case "14":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS14);
                                }
                                else
                                {
                                    SetText("0", textBoxS14);
                                }
                            }
                            break;
                        case "15":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS15);
                                }
                                else
                                {
                                    SetText("0", textBoxS15);
                                }
                            }
                            break;
                        case "16":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS16);
                                }
                                else
                                {
                                    SetText("0", textBoxS16);
                                }
                            }
                            break;
                        case "17":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS17);
                                }
                                else
                                {
                                    SetText("0", textBoxS17);
                                }
                            }
                            break;
                        case "18":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS18);
                                }
                                else
                                {
                                    SetText("0", textBoxS18);
                                }
                            }
                            break;
                        case "19":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS19);
                                }
                                else
                                {
                                    SetText("0", textBoxS19);
                                }
                            }
                            break;
                        case "20":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxS20);
                                }
                                else
                                {
                                    SetText("0", textBoxS20);
                                }
                            }
                            break;
                    }
                }
                else if (message.Contains("getValve:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    switch (arguments[1])
                    {
                        case "1":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV1);
                                }
                                else
                                {
                                    SetText("0", textBoxV1);
                                }
                            }
                            break;
                        case "2":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV2);
                                }
                                else
                                {
                                    SetText("0", textBoxV2);
                                }
                            }
                            break;
                        case "3":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV3);
                                }
                                else
                                {
                                    SetText("0", textBoxV3);
                                }
                            }
                            break;
                        case "4":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV4);
                                }
                                else
                                {
                                    SetText("0", textBoxV4);
                                }
                            }
                            break;
                        case "5":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV5);
                                }
                                else
                                {
                                    SetText("0", textBoxV5);
                                }
                            }
                            break;
                        case "6":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxV6);
                                }
                                else
                                {
                                    SetText("0", textBoxV6);
                                }
                            }
                            break;
                    }
                }
                else if (message.Contains("getPump:"))
                {
                    string[] arguments = message.Split(delimiterChars);
                    switch (arguments[1])
                    {
                        case "1":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxP1);
                                }
                                else
                                {
                                    SetText("0", textBoxP1);
                                }
                            }
                            break;
                        case "2":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxP2);
                                }
                                else
                                {
                                    SetText("0", textBoxP2);
                                }
                            }
                            break;
                        case "3":
                            {
                                if (arguments[2] == "On")
                                {
                                    SetText("1", textBoxP3);
                                }
                                else
                                {
                                    SetText("0", textBoxP3);
                                }
                            }
                            break;
                    }
                }
                else if (message.Contains("calSIT done"))
                {
                    SetText(message, textBoxFLDMsg);
                }
                else
                {
                    SetText(message, textBoxFLDMsg);
                }

            }
            catch (TimeoutException) { }
        }
        private Task<string> ExecuteAsync(FluidicsCommand command)
        {
            if (_Device.IsOpen() == false) return Task.FromResult("Not connect");

            _FluidicsCommandExecutionTransformBlock.Post(command);

            Func<Task<bool>, string> act = new Func<Task<bool>, string>((task) =>
            {
                try
                {
                    if (!task.Result)
                    {
                        throw new Exception(string.Format("Fluidics command {0} timed out!", command.Command.Msg), task.Exception?.GetBaseException());
                    }
                    return command.ResponseString;
                }
                catch (Exception e)
                {
                    // Get exception that 
                    var aggregateException = _FluidicsCommandWaiterTransformBlock.Completion.Exception;
                    lock (_LockObject) { _PortReturnMessage = null; }
                    if (aggregateException != null)
                    {
                        AllocationCommandProcessingBlocks();
                        throw aggregateException.GetBaseException();
                    }
                    else
                    {
                        AllocationCommandProcessingBlocks();
                        throw;
                    }
                }
            });

            return _FluidicsCommandWaiterTransformBlock.ReceiveAsync().ContinueWith(act);
        }
        private bool WaitCommand(FluidicsCommand command)
        {
            bool isCommandTimedout = false;
            var desiredReturnMessage = new List<string>(command.Command.ReturnMessage);
            string msg = null;
            while (true)
            {
                var deadline = command.CommandStartTime.Add(command.Command.Timeout);


                var timeout = deadline.Subtract(DateTime.Now);
                if (deadline.CompareTo(DateTime.Now) < 0)
                {
                    //Give another 50 milliseconds to check the result is in the _portReturnMessage list or not.
                    //Especially during debug, deadline is less than DateTime.Now, but the command's result is in the _portReturnMessage list.
                    timeout = TimeSpan.FromMilliseconds(50);
                }

                try
                {
                    if (_PortReturnMessage.TryTake(out msg, (int)timeout.TotalMilliseconds, _DisconnectCancellationTokenSource.Token))
                    {
                        if (!string.IsNullOrEmpty(msg))
                        {
                            command.ResponseString = msg;
                            desiredReturnMessage.RemoveAll((p) => msg.Contains(p));
                        }

                        if (desiredReturnMessage.Any() == false)
                        {
                            // totally match
                            lock (_LockObject) { _PortReturnMessage = null; }
                            break;
                        }
                    }
                    else
                    {
                        // timeout
                        isCommandTimedout = true;
                        break;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    isCommandTimedout = true;
                    break;
                }
            }

            return isCommandTimedout == false;
        }
        private void AllocationCommandProcessingBlocks()
        {
            _PortReturnMessage = new BlockingCollection<string>();
            _FluidicsCommandExecutionTransformBlock = new TransformBlock<FluidicsCommand, FluidicsCommand>((cmd) =>
            {
                lock (_LockObject)
                {
                    _PortReturnMessage = new BlockingCollection<string>();
                    cmd.Execute(_Device);
                    return cmd;
                }
            });

            _FluidicsCommandWaiterTransformBlock = new TransformBlock<FluidicsCommand, bool>((cmd) =>
            {
                return WaitCommand(cmd);
            });

            _FluidicsCommandExecutionTransformBlock.LinkTo(_FluidicsCommandWaiterTransformBlock, new DataflowLinkOptions { PropagateCompletion = true });
        }

        private void SendMultipleCountModel_Click(object sender, EventArgs e)
        {
            var index = listBoxModes.SelectedIndex;
            Task.Run(() =>
            {
                for (int i = 0; i < int.Parse(textBoxExecuteCount.Text); i++)
                {
                    SendModelTable(index);
                    var count = i + 1;
                    SetText(count.ToString(), textBoxSendCount);
                }
            });
        }
    }
    class FluidicsMode : FluidicsCommand
    {
        private int _ModeNum;
        public FluidicsMode(int modeNum)
            : base(Command.FromCode(Command.EXEC_MODE))
        {
            _ModeNum = modeNum;
        }

        public override void Execute(IFluidicsDevice device)
        {
            if (device != null && device.IsOpen())
            {
                try
                {
                    SendMessageTransaction(device, Command.Code);
                    device.WriteBytesOff(new byte[] { (byte)_ModeNum }, 0, 1);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Error {0}", Command.Msg), e);
                }
            }
        }
    }
    class ByteCodeWithoutArg : FluidicsCommand
    {
        public ByteCodeWithoutArg(byte code) : base(Command.FromCode(code))
        {
        }

        public override void Execute(IFluidicsDevice device)
        {
            if (device != null && device.IsOpen())
            {
                try
                {
                    SendMessageTransaction(device, Command.Code);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Error execute command {0}", string.Join(",", Command.ReturnMessage)), e);
                }
            }
        }
    }
    class ByteCodeWithArgs : FluidicsCommand
    {
        private float[] _Arguments;
        public ByteCodeWithArgs(byte code, float[] args) : base(Command.FromCode(code))
        {
            _Arguments = args;
        }

        public override void Execute(IFluidicsDevice device)
        {
            if (device != null && device.IsOpen())
            {
                try
                {
                    SendMessageTransaction(device, Command.Code);

                    var buffer = new byte[_Arguments.Length * sizeof(float)];
                    Buffer.BlockCopy(_Arguments, 0, buffer, 0, buffer.Length);
                    device.WriteBytesOff(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Error execute command {0}, with args {1}", string.Join(",", Command.ReturnMessage), string.Join(",", _Arguments)), e);
                }
            }
        }
    }
    public class SerialPortFluidicsDevice : IFluidicsDevice
    {
        public event EventHandler<FluidicsPortMessageEventArgs> PortMessageReceived;
        private SerialPort _SerialPort;
        public SerialPortFluidicsDevice(string portName)
        {

            _SerialPort = new SerialPort();
            _SerialPort.PortName = portName;
            _SerialPort.BaudRate = 512000;
            _SerialPort.WriteTimeout = 10000;

            _SerialPort.Open();
            _SerialPort.DataReceived += SerialPort_DataReceived;

        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {

                SerialPort port = (SerialPort)sender;
                int len = port.BytesToRead;
                byte[] buf = new byte[len];
                port.Read(buf, 0, buf.Length);
                Buffer.BlockCopy(buf, 0, LineBuffer, LineBufPointer, len);
                string str = Encoding.ASCII.GetString(buf);
                var retMsg = str.Split(new char[] { '\n' });
                LineBufPointer += len;
                //foreach (var msg in retMsg)
                //{
                PortMessageReceived?.Invoke(this, new FluidicsPortMessageEventArgs(str));
                //}

                LineBufPointer = 0;
            }
            catch (Exception ex)
            {

            }
        }
        public void WriteBytesOff(byte[] data, int offset, int count)
        {

            if (_SerialPort.IsOpen == false) _SerialPort.Open();
            _SerialPort.Write(data, offset, count);
        }
        public bool IsOpen()
        {
            return _SerialPort?.IsOpen == true;
        }
        private int LineBufPointer = 0;
        private byte[] LineBuffer = new byte[1024];
        public void Dispose()
        {
            _SerialPort.DataReceived -= SerialPort_DataReceived;
            _SerialPort.Close();
            _SerialPort.Dispose();
            _SerialPort = null;
        }

    }
    public class FluidicsPortMessageEventArgs : EventArgs
    {
        public FluidicsPortMessageEventArgs(string msg)
        {
            Message = msg;
        }

        public string Message { get; private set; }
    }

    public interface IFluidicsDevice
    {
        event EventHandler<FluidicsPortMessageEventArgs> PortMessageReceived;
        void Dispose();
        bool IsOpen();
        void WriteBytesOff(byte[] data, int offset, int count);
    }
    public abstract class FluidicsCommand
    {
        public Command Command { get; private set; }
        public string ResponseString { get; set; }
        public DateTime CommandStartTime { get; private set; } = DateTime.MinValue;
        public abstract void Execute(IFluidicsDevice device);

        public FluidicsCommand(Command command)
        {
            Command = command;
        }

        protected void SendMessageTransaction(IFluidicsDevice device, byte cmdMsg)
        {
            CommandStartTime = DateTime.Now;
            byte[] msg = new byte[1];
            msg[0] = cmdMsg;
            device.WriteBytesOff(msg, 0, 1);
        }
    }
    public class AsyncPump
    {
        private SemaphoreSlim semaphore;
        private CancellationTokenSource _CancelTokenSource;

        public AsyncPump()
        {
            semaphore = new SemaphoreSlim(1);
            _CancelTokenSource = new CancellationTokenSource();
        }

        public void Cancel()
        {
            _CancelTokenSource.Cancel();
            _CancelTokenSource = new CancellationTokenSource();
        }

        public async Task<T> Enqueue<T>(Func<Task<T>> taskGenerator)
        {
            await semaphore.WaitAsync();

            try
            {
                return await taskGenerator().ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private Task<T> RunExclusively<T>(Func<Task<T>> func)
        {
            semaphore.Wait(_CancelTokenSource.Token);
            Task<T> executeTask = null;
            try
            {
                executeTask = func();
                if (executeTask != null)
                {
                    executeTask.ContinueWith((t) =>
                    {
                        semaphore.Release();
                    }, TaskScheduler.Default);
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                if (executeTask == null)
                {
                    semaphore.Release();
                }
            }
            return executeTask;
        }

        /// <summary>Runs the specified asynchronous function.</summary>
        /// <param name="func">The asynchronous function to execute.</param>
        public T Run<T>(Func<Task<T>> func, int timeoutMillis)
        {
            if (func == null) throw new ArgumentNullException("func");

            var task = RunExclusively(func);
            try
            {
                var token = _CancelTokenSource.Token;
                task.Wait(timeoutMillis, token);
                return task.Result;
            }
            catch (AggregateException ex)
            {
                throw ex.GetBaseException();
            }
            catch (OperationCanceledException ex)
            {
                if (semaphore.CurrentCount == 0)
                    semaphore.Release();
                throw ex;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public Task<T> DelegateRun<T>(Func<Task<T>> func, int timeoutMillis)
        {
            if (func == null) throw new ArgumentNullException("func");
            return RunExclusively(func);
        }
    }
    public class FlowRateCalibrationInfo
    {
        public string Name { get; set; }
        public float Target { get; set; }
        public float Spec { get; set; }
        public int ModelTableIndex { get; set; }
    }
    public class Command
    {
        // MSG Codes
        public const byte LOAD_MODE_TABLE = 0x11;
        public const byte DUMMY_MESSAGE = 0x12;
        public const byte E_STOP = 0x13;
        public const byte SET_PRESS = 0x14;
        public const byte GET_PRESS = 0x15;
        public const byte SET_PRESS_TOL = 0x16;
        public const byte GET_PRESS_TOL = 0x17;
        public const byte SET_REF_TEMP = 0x18;
        public const byte GET_REF_TEMP = 0x34;
        public const byte GET_TEMP = 0x19;
        public const byte SET_TEMP_FACTOR = 0x1A;
        public const byte GET_TEMP_FACTOR = 0x1B;
        public const byte EXEC_MODE = 0x1C;
        public const byte SET_CAL_VALUES = 0x1D;
        public const byte GET_CAL_VALUES = 0x26;
        public const byte GET_SENSOR = 0x1E;
        public const byte GET_VALVE = 0x1F;
        public const byte GET_PUMP = 0x20;
        public const byte SET_VALVE = 0x21;
        public const byte SET_PUMP = 0x22;
        public const byte SET_LASER = 0x23;
        public const byte GET_LASER = 0x24;
        public const byte GET_LASER_DELAY = 0x52;
        public const byte SET_LASER_DELAY = 0x25;
        public const byte INCREASE_FLOW_RATE = 0x29;
        public const byte DECREASE_FLOW_RATE = 0x27;
        public const byte GET_SAMPLE = 0x28;
        public const byte SET_FLOW_RATE = 0x30;
        public const byte READ_SENSIRION = 0x33;
        public const byte SET_CONTROLLER = 0x35;
        public const byte SET_PID = 0x36;
        public const byte GET_PID = 0x37;
        public const byte GET_SIT_POS = 0x40;
        public const byte GET_SIT_TOL = 0x42;
        public const byte SET_SIT_TOL = 0x41;
        public const byte CALIBRATE_SIT = 0x43;
        public const byte MOVE_SIT = 0x44;
        public const byte HOME_SIT = 0x45;

        public const byte SET_TUBE_K = 0x48;
        public const byte GET_TUBE_K = 0x49;
        public const byte SET_NOTUBE_K = 0x4A;
        public const byte GET_NOTUBE_K = 0x4B;

        public const byte SET_SHUTTER = 0x50;
        public const byte GET_VERSION = 0xD0;

        private string _Msg;
        private byte _Code;
        private bool _Args;
        private bool _HasArguments;

        public string Msg
        {
            get
            {
                return _Msg;
            }
            set
            {
                _Msg = value;
            }
        }

        public byte Code
        {
            get
            {
                return _Code;
            }
            set
            {
                _Code = value;
            }
        }

        public bool Args
        {
            get
            {
                return _Args;
            }
        }
        public TimeSpan Timeout { get; private set; }
        public string[] ReturnMessage { get; private set; }
        public bool HasArguments
        {
            get { return _HasArguments; }
        }

        public Command(String msg, byte code, bool args)
        {
            _Msg = msg;
            _Code = code;
            _Args = args;
        }
        private Command(string msg, byte code, bool hasArgs, TimeSpan timeout, string[] returnMessage)
        {
            _Msg = msg;
            _Code = code;
            _HasArguments = hasArgs;
            Timeout = timeout;
            ReturnMessage = returnMessage;
        }

        private static readonly Dictionary<byte, string[]> COMMAND_RETURN = new Dictionary<byte, string[]> {
            { LOAD_MODE_TABLE, new string[] { "Initialization Done" } },
            { DUMMY_MESSAGE, new string[] { "Don't be such a dummy" } },
            { E_STOP, new string[] { "eStop: Received" } },
            { SET_PRESS, new string[] { "setPress:" } },
            { GET_PRESS, new string[] { "getPres:" } },
            { SET_PRESS_TOL, new string[] { "setPresTol:" } },
            { GET_PRESS_TOL, new string[] { "getPresTol:" } },
            { SET_REF_TEMP, new string[] { "setRefTemp:" } },
            { GET_REF_TEMP, new string[] { "getRefTemp:" } },
            { GET_TEMP, new string[] { "getTemp:" } },
            { SET_TEMP_FACTOR, new string[] { "setTempFactor:" } },
            { GET_TEMP_FACTOR, new string[] { "getTempFactor:" } },
            { EXEC_MODE, new string[] { "execModeBegin", "execModeEnd" } },
            { SET_CAL_VALUES, new string[] { "SetCalValues received" } },
            { GET_CAL_VALUES, new string[] { "Calibration values:" } },
            { GET_SENSOR, new string[] { "getSensor:" } },
            { GET_VALVE, new string[] { "getValve:" } },
            { GET_PUMP, new string[] { "getPump:" } },
            { SET_VALVE, new string[] { "setValve:" } },
            { SET_PUMP, new string[] { "setPump:" } },
            { SET_LASER, new string[] { "setLaser:" } },
            { GET_LASER, new string[] { "getLaser:" } },
            { SET_LASER_DELAY, new string[] { "setLaserDelay:" } },
            { INCREASE_FLOW_RATE, new string[] { "getFlowRate:" } },
            { DECREASE_FLOW_RATE, new string[] { "getFlowRate:" } },
            { GET_SAMPLE, new string[] { "getSample:" } },
            { SET_FLOW_RATE, new string[] { "getFlowRate:" } },
            { READ_SENSIRION, new string[] { "readSensirion:" } },
            { GET_SIT_POS, new string[] { "getSIT pos:" } },
            { GET_SIT_TOL, new string[] { "getSIT tolerance:" } },
            { SET_SIT_TOL, new string[] { "setSITtolerance:" } },
            { CALIBRATE_SIT, new string[] { "calSIT" } },
            { MOVE_SIT, new string[] { "Run finish" } },
            { GET_VERSION, new string[] { "getVersion:" } },
            { SET_SHUTTER, new string[] { "setShutter:" } },
            { HOME_SIT, new string[] { "homeSIT done"} },
            { SET_TUBE_K, new string[]{ "setTubeK" } },
            { GET_TUBE_K, new string[] { "getTubeK:" } },
            { SET_NOTUBE_K, new string[]{ "setNoTubeK" } },
            { GET_NOTUBE_K, new string[] { "getNoTubeK:" } },
            { GET_LASER_DELAY, new string[] { "getLaserDelay:" } },
            //{ GET_LASER_MON, new string[] { "getLaserMon:" } },
            //{ SET_DAC_TEST, new string[] { "setDACTest" } }
        };

        public static List<Command> Commands = new List<Command>
            {
                new Command("getVersion", GET_VERSION, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_VERSION]),
                new Command("getLaserDelay", GET_LASER_DELAY, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_LASER_DELAY]),
                new Command("eStop", E_STOP, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[E_STOP]),
                new Command("setPress", SET_PRESS, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_PRESS]),
                new Command("getPress", GET_PRESS, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_PRESS]),
                new Command("getSample", GET_SAMPLE, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_SAMPLE]),
                new Command("setPresTol", SET_PRESS_TOL, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_PRESS_TOL]),
                new Command("getPresTol", GET_PRESS_TOL, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_PRESS_TOL]),
                new Command("setRefTemp", SET_REF_TEMP, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_REF_TEMP]),
                new Command("getRefTemp", GET_REF_TEMP, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_REF_TEMP]),
                new Command("getTemp", GET_TEMP, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_TEMP]),
                new Command("setTempFactor", SET_TEMP_FACTOR, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_TEMP_FACTOR]),
                new Command("getTempFactor", GET_TEMP_FACTOR, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_TEMP_FACTOR]),
                new Command("execMode", EXEC_MODE, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[EXEC_MODE]),
                new Command("setCalValues", SET_CAL_VALUES, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_CAL_VALUES]),
                new Command("getCalValues", GET_CAL_VALUES, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_CAL_VALUES]),
                new Command("getSens", GET_SENSOR, true, TimeSpan.FromMilliseconds(20000), COMMAND_RETURN[GET_SENSOR]),
                new Command("getValve", GET_VALVE, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_VALVE]),
                new Command("getPump", GET_PUMP, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_PUMP]),
                new Command("setValve", SET_VALVE, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_VALVE]),
                new Command("setPump", SET_PUMP, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_PUMP]),
                new Command("getLaser", GET_LASER, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_LASER]),
                new Command("setLaser", SET_LASER, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_LASER]),
                new Command("setLaserDelay", SET_LASER_DELAY, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_LASER_DELAY]),
                new Command("increaseFlowRate", INCREASE_FLOW_RATE, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[INCREASE_FLOW_RATE]),
                new Command("decreaseFlowRate", DECREASE_FLOW_RATE, true, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[DECREASE_FLOW_RATE]),
                new Command("ReadSensirion", READ_SENSIRION, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[READ_SENSIRION]),
                new Command("getSITtolerance", GET_SIT_TOL, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_SIT_TOL]),
                new Command("getSITposition", GET_SIT_POS, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[GET_SIT_POS]),
                new Command("setSITtolerance", SET_SIT_TOL, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_SIT_TOL]),
                new Command("calSIT", CALIBRATE_SIT, false, TimeSpan.FromMilliseconds(60000), COMMAND_RETURN[CALIBRATE_SIT]),
                new Command("moveSIT", MOVE_SIT, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[MOVE_SIT]),
                new Command("setShutter", SET_SHUTTER, false, TimeSpan.FromMilliseconds(600000), COMMAND_RETURN[SET_SHUTTER]),
                new Command("homeSIT", HOME_SIT, false, TimeSpan.FromMilliseconds(60000), COMMAND_RETURN[HOME_SIT])
            };
        public static Command FromCode(byte code)
        {
            if (code == LOAD_MODE_TABLE)
            {
                return new Command("load mode table", LOAD_MODE_TABLE, false, TimeSpan.FromSeconds(20), COMMAND_RETURN[LOAD_MODE_TABLE]);
            }
            else if (code == DUMMY_MESSAGE)
            {
                return new Command("dummy message", DUMMY_MESSAGE, false, TimeSpan.FromSeconds(5), COMMAND_RETURN[DUMMY_MESSAGE]);
            }
            else if (code == SET_FLOW_RATE)
            {
                return new Command("setFlowRate", SET_FLOW_RATE, false, TimeSpan.FromSeconds(300), COMMAND_RETURN[SET_FLOW_RATE]);
            }
            else if (code == GET_VERSION)
            {
                return new Command("getVersion", GET_VERSION, false, TimeSpan.FromSeconds(300), COMMAND_RETURN[GET_VERSION]);
            }
            else if (code == SET_TUBE_K)
            {
                return new Command("setTubeK", SET_TUBE_K, false, TimeSpan.FromSeconds(20), COMMAND_RETURN[SET_TUBE_K]);
            }
            else if (code == GET_TUBE_K)
            {
                return new Command("getTubeK", GET_TUBE_K, false, TimeSpan.FromSeconds(20), COMMAND_RETURN[GET_TUBE_K]);
            }
            else if (code == SET_NOTUBE_K)
            {
                return new Command("setNoTubeK", SET_NOTUBE_K, false, TimeSpan.FromSeconds(300), COMMAND_RETURN[SET_NOTUBE_K]);
            }
            else if (code == GET_NOTUBE_K)
            {
                return new Command("getNoTubeK", GET_NOTUBE_K, false, TimeSpan.FromSeconds(300), COMMAND_RETURN[GET_NOTUBE_K]);
            }
            //else if (code == GET_LASER_MON)
            //{
            //    return new Command("getLaserMon", GET_LASER_MON, false, TimeSpan.FromSeconds(60), COMMAND_RETURN[GET_LASER_MON]);
            //}
            else
            {
                return Commands.Find(p => p.Code == code);
            }
        }
    }

    public struct FluidicsVersion
    {
        public string VersionString { get; private set; }
        public int MajorVersion { get; private set; }
        public int MinorVersion { get; private set; }
        public int BuildNumber { get; private set; }
        public int Revison { get; private set; }
        public string RevisonStr { get; private set; }

        public FluidicsVersion(string versionString)
        {
            VersionString = versionString;
            MajorVersion = 0;
            MinorVersion = 0;
            BuildNumber = 0;
            Revison = 0;
            RevisonStr = string.Empty;
            ParseVersion(versionString);
        }

        private void ParseVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
            {
                MajorVersion = 0;
                MinorVersion = 0;
                BuildNumber = 0;
                Revison = 0;
                RevisonStr = string.Empty;
            }
            else
            {
                var splitedRevisonString = versionString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitedRevisonString.Count() > 1)
                {
                    RevisonStr = splitedRevisonString[1];
                    if (int.TryParse(splitedRevisonString[1], out int revison))
                    {
                        Revison = revison;
                    }
                    else
                    {
                        Revison = 0;
                    }
                }
                else
                {
                    RevisonStr = string.Empty;
                    Revison = 0;
                }

                var splitedVersion = splitedRevisonString[0].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                for (int index = 0; index < splitedVersion.Count(); index++)
                {
                    int versionNum = 0;
                    if (int.TryParse(splitedVersion[index], out versionNum) == false)
                    {
                        versionNum = 0;
                    }

                    if (index == 0)
                    {
                        MajorVersion = versionNum;
                    }
                    else if (index == 1)
                    {
                        MinorVersion = versionNum;
                    }
                    else if (index == 2)
                    {
                        BuildNumber = versionNum;
                    }
                }
            }
        }

        public int GetFirmwareVersionId()
        {
            return GetFirmwareVersionId(VersionString);
        }

        public static bool operator >=(FluidicsVersion version1, FluidicsVersion version2)
        {
            if (version1.MajorVersion > version2.MajorVersion)
            {
                return true;
            }
            else if (version1.MajorVersion == version2.MajorVersion)
            {
                if (version1.MinorVersion > version2.MinorVersion)
                {
                    return true;
                }
                else if (version1.MinorVersion == version2.MinorVersion)
                {
                    if (version1.BuildNumber > version2.BuildNumber)
                    {
                        return true;
                    }
                    else if (version1.BuildNumber == version2.BuildNumber)
                    {
                        return version1.Revison >= version2.Revison;
                    }
                }
            }
            return false;
        }
        public static bool operator <=(FluidicsVersion version1, FluidicsVersion version2)
        {
            if (version1.MajorVersion < version2.MajorVersion)
            {
                return true;
            }
            else if (version1.MajorVersion == version2.MajorVersion)
            {
                if (version1.MinorVersion < version2.MinorVersion)
                {
                    return true;
                }
                else if (version1.MinorVersion == version2.MinorVersion)
                {
                    if (version1.BuildNumber < version2.BuildNumber)
                    {
                        return true;
                    }
                    else if (version1.BuildNumber == version2.BuildNumber)
                    {
                        return version1.Revison <= version2.Revison;
                    }
                }
            }
            return false;
        }

        public static bool operator >(FluidicsVersion version1, FluidicsVersion version2)
        {
            if (version1.MajorVersion > version2.MajorVersion)
            {
                return true;
            }
            else if (version1.MajorVersion == version2.MajorVersion)
            {
                if (version1.MinorVersion > version2.MinorVersion)
                {
                    return true;
                }
                else if (version1.MinorVersion == version2.MinorVersion)
                {
                    if (version1.BuildNumber > version2.BuildNumber)
                    {
                        return true;
                    }
                    else if (version1.BuildNumber == version2.BuildNumber)
                    {
                        return version1.Revison > version2.Revison;
                    }
                }
            }
            return false;
        }
        public static bool operator <(FluidicsVersion version1, FluidicsVersion version2)
        {
            if (version1.MajorVersion < version2.MajorVersion)
            {
                return true;
            }
            else if (version1.MajorVersion == version2.MajorVersion)
            {
                if (version1.MinorVersion < version2.MinorVersion)
                {
                    return true;
                }
                else if (version1.MinorVersion == version2.MinorVersion)
                {
                    if (version1.BuildNumber < version2.BuildNumber)
                    {
                        return true;
                    }
                    else if (version1.BuildNumber == version2.BuildNumber)
                    {
                        return version1.Revison < version2.Revison;
                    }
                }
            }
            return false;
        }

        public static bool operator ==(FluidicsVersion version1, FluidicsVersion version2)
        {
            return version1.MajorVersion == version2.MajorVersion
                    && version1.MinorVersion == version2.MinorVersion
                    && version1.BuildNumber == version2.BuildNumber
                    && version1.Revison == version2.Revison;
        }

        public static bool operator !=(FluidicsVersion version1, FluidicsVersion version2)
        {
            return false == (version1 == version2);
        }

        public override bool Equals(object obj)
        {
            if (obj is FluidicsVersion otherFluidicsVersion)
            {
                return $"{MajorVersion}{MinorVersion}{BuildNumber}{Revison}".Equals($"{otherFluidicsVersion.MajorVersion}{otherFluidicsVersion.MinorVersion}{otherFluidicsVersion.BuildNumber}{otherFluidicsVersion.Revison}");
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return $"{MajorVersion}{MinorVersion}{BuildNumber}{Revison}".GetHashCode();
        }

        public static int GetFirmwareVersionId(string versionString)
        {
            int version = 0;
            var firstSplit = versionString.Split(' ');
            var versionNumber = firstSplit[0];
            var versionPostfix = firstSplit.Length > 1 ? firstSplit[1] : "";

            var versionNumberSplits = versionNumber.Split('.');
            int count = versionNumberSplits.Length;
            for (int i = 0; i <= count - 1; i++)
            {
                int n = int.Parse(versionNumberSplits[i]);
                int exponent = (i != count - 1) ? count - i : count - i - 1;
                version += n * ((int)Math.Pow(10, exponent));
            }

            return version;
        }
    }
}
