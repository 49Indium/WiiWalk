// Wii Walk by Isaac Beh (49Indium)
// Based off WiiBalanceWalker v0.5, by Shachar Liberman
// Which was derived from Richard Perry from GreyCube.com - Under the Microsoft Public License


using System;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Forms;
using System.Collections.Generic;
using WiimoteLib;

using System.Text;

namespace WiiBalanceWalker
{

    public partial class FormMain : Form
    {
        // Causes updates to keypresses
        System.Timers.Timer infoUpdateTimer = new System.Timers.Timer() { Interval = 18, Enabled = false };

        // Jumping parameters
        public const double maxJumpTime = 0.6;

        // Walking & sprinting parameters
        public const double walkStartTime = 0.45;
        public const double walkEndTime = 0.4;
        public const double sprintStartTime = 0.15;
        public const double sprintEndTime = 0.25;
        public const double walkContinuationTime = 1.3;

        // Turn Certain movements on or off
        public const bool walkingOn = true;
        public const bool sprintingOn = true;
        public const bool turningOn = true;
        public const bool turningVerticalOn = true;
        public const bool jumpingOn = true;

        // Tilting parameters
        // Speed is rate at which a percentage weight shift leads to action
        // Maximum is the maximum rate at which the action can occur
        public const double turningNullZonePercentage = 17.0;
        public const double tiltSpeed = 1.15;
        public const double tiltMax = 8.0;
        public const double turningNullZonePercentageMoving = 20.0;
        public const double tiltSpeedMoving = 1.25;
        public const double tiltMaxMoving = 15.0;
        public const double turningNullZonePercentageVertical = 9.0;
        public const double tiltSpeedVertical = 1.05;
        public const double tiltMaxVertical = -4.0;
        public const double turningNullZonePercentageVerticalMoving = 25;
        public const double tiltSpeedVerticalMoving = 1.05;
        public const double tiltMaxVerticalMoving = -2.0;

        // The number of measurements to smooth over
        public const int maxAverageCount = 12;
        public const int maxAverageCountVertical = 15;

        ActionList actionList = new ActionList();
        Wiimote wiiBalanceBoard = new Wiimote();
        Wiimote wiiStick = new Wiimote();
        DateTime lastGroundTime = DateTime.UtcNow;

        // The last change from left to right
        DateTime lastFootSwitchTime = DateTime.UtcNow;

        // The last change in any state (including none)
        DateTime lastFootChangeStateTime = DateTime.UtcNow;
        DateTime lastWalkTime = DateTime.UtcNow;
        DateTime lastSprintTime = DateTime.UtcNow;

        DateTime jumpTime = DateTime.UtcNow;

        enum Foot
        {
            Left, Right, None
        }
        // The last foot registed to be down (when the other foot is up)
        Foot lastFoot = Foot.None;

        // State of the player
        bool isWalking = false;
        bool isSprinting = false;
        bool isJumping = false;
        bool wasOnBalanceBoardLastTick = false;

        // For smoothing of tilting
        Queue<double> horizontalCenteringQueue = new Queue<double>();
        double horizontalCenteringAverage = 0;

        Queue<double> verticalCenteringQueue = new Queue<double>();
        double verticalCenteringAverage = 0;


        bool setCenterOffset = false;
        bool resetCenterOffsetPossible = false;

        float naCorners = 0f;
        float oaTopLeft = 0f;
        float oaTopRight = 0f;
        float oaBottomLeft = 0f;
        float oaBottomRight = 0f;

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // Setup a timer which controls the rate at which updates are processed.

            infoUpdateTimer.Elapsed += new ElapsedEventHandler(infoUpdateTimer_Elapsed);

            // Load trigger settings.

            numericUpDown_TLR.Value = Properties.Settings.Default.TriggerLeftRight;
            numericUpDown_TFB.Value = Properties.Settings.Default.TriggerForwardBackward;
            numericUpDown_TMLR.Value = Properties.Settings.Default.TriggerModifierLeftRight;
            numericUpDown_TMFB.Value = Properties.Settings.Default.TriggerModifierForwardBackward;

            // Link up form controls with action settings.

            actionList.Left = new ActionItem("Left", comboBox_AL, numericUpDown_AL);
            actionList.Right = new ActionItem("Right", comboBox_AR, numericUpDown_AR);
            actionList.Forward = new ActionItem("Forward", comboBox_AF, numericUpDown_AF);
            actionList.Backward = new ActionItem("Backward", comboBox_AB, numericUpDown_AB);
            actionList.Modifier = new ActionItem("Modifier", comboBox_AM, numericUpDown_AM);
            actionList.Jump = new ActionItem("Jump", comboBox_AJ, numericUpDown_AJ);
            actionList.DiagonalLeft = new ActionItem("DiagonalLeft", comboBox_ADL, numericUpDown_ADL);
            actionList.DiagonalRight = new ActionItem("DiagonalRight", comboBox_ADR, numericUpDown_ADR);

            // Load saved preference.

            checkBox_SendCGtoXY.Checked = Properties.Settings.Default.SendCGtoXY;
            checkBox_Send4LoadSensors.Checked = Properties.Settings.Default.Send4LoadSensors;
            checkBox_DisableActions.Checked = Properties.Settings.Default.DisableActions;
            checkBox_StartMinimized.Checked = Properties.Settings.Default.StartMinimized;

            if (checkBox_StartupAutoConnect.Checked)
            {
                button_Connect.PerformClick();
            }

            if (checkBox_StartMinimized.Checked)
            {
                this.WindowState = FormWindowState.Minimized;
            }
        }

        private void numericUpDown_TLR_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerLeftRight = (int)numericUpDown_TLR.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TFB_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerForwardBackward = (int)numericUpDown_TFB.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TMLR_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerModifierLeftRight = (int)numericUpDown_TMLR.Value;
            Properties.Settings.Default.Save();
        }

        private void numericUpDown_TMFB_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.TriggerModifierForwardBackward = (int)numericUpDown_TMFB.Value;
            Properties.Settings.Default.Save();
        }

        private void button_SetCenterOffset_Click(object sender, EventArgs e)
        {
            if (resetCenterOffsetPossible && wiiBalanceBoard.WiimoteState.BalanceBoardState.WeightKg <= 5)
            {
                naCorners = 0f;
                oaTopLeft = 0f;
                oaTopRight = 0f;
                oaBottomLeft = 0f;
                oaBottomRight = 0f;
                button_SetCenterOffset.Enabled = false;
                resetCenterOffsetPossible = false;
                button_SetCenterOffset.Text = "Set current balance as center";
                //toolTip1.SetToolTip(button_SetCenterOffset, "While standing or sitting on the balance board, click this button to set your current balance point as center");
            }

            else
            {
                setCenterOffset = true;
                //toolTip1.SetToolTip(button_SetCenterOffset, "Revert back to the original center balance point, for the X/Y controls");
            }
        }

        private void button_ResetDefaults_Click(object sender, EventArgs e)
        {
            //Properties.Settings.Default.Reset();
            this.Close();
        }

        private void button_BluetoothAddDevice_Click(object sender, EventArgs e)
        {
            var form = new FormBluetooth();
            form.ShowDialog(this);
        }

        private void button_Connect_Click(object sender, EventArgs e)
        {
            try
            {
                // Find all connected Wii devices.

                var deviceCollection = new WiimoteCollection();
                deviceCollection.FindAllWiimotes();

                for (int i = 0; i < deviceCollection.Count; i++)
                {
                    wiiBalanceBoard = deviceCollection[i];

                    // Device type can only be found after connection, so prompt for multiple devices.

                    if (deviceCollection.Count > 1)
                    {
                        var devicePathId = new Regex("e_pid&.*?&(.*?)&").Match(wiiBalanceBoard.HIDDevicePath).Groups[1].Value.ToUpper();

                        var response = MessageBox.Show("Connect Wii Balance Board to HID " + devicePathId + " device " + (i + 1) + " of " + deviceCollection.Count + " ?", "Multiple Wii Devices Found", MessageBoxButtons.YesNoCancel);
                        if (response == DialogResult.Cancel) return;
                        if (response == DialogResult.No) continue;
                    }
                }

                // for (int i = 0; i < deviceCollection.Count; i++)
                // {
                //     wiiStick = deviceCollection[i];

                //     // Device type can only be found after connection, so prompt for multiple devices.

                //     if (deviceCollection.Count > 1)
                //     {
                //         var devicePathId = new Regex("e_pid&.*?&(.*?)&").Match(wiiStick.HIDDevicePath).Groups[1].Value.ToUpper();

                //         var response = MessageBox.Show("Connect Wii Remote (stick) to HID " + devicePathId + " device " + (i + 1) + " of " + deviceCollection.Count + " ?", "Multiple Wii Devices Found", MessageBoxButtons.YesNoCancel);
                //         if (response == DialogResult.Cancel) return;
                //         if (response == DialogResult.No) continue;
                //     }
                // }

                // Setup update handlers.

                wiiBalanceBoard.WiimoteChanged += wiiDevice_WiimoteChanged;
                wiiBalanceBoard.WiimoteExtensionChanged += wiiDevice_WiimoteExtensionChanged;

                // wiiStick.WiimoteChanged += wiiDevice_WiimoteChanged;
                // wiiStick.WiimoteExtensionChanged += wiiDevice_WiimoteExtensionChanged;

                // Connect and send a request to verify it worked.

                wiiBalanceBoard.Connect();
                wiiBalanceBoard.SetReportType(InputReport.IRAccel, false); // FALSE = DEVICE ONLY SENDS UPDATES WHEN VALUES CHANGE!
                wiiBalanceBoard.SetLEDs(true, false, false, false);

                // wiiStick.Connect();
                // wiiStick.SetReportType(InputReport.IRAccel, false); // FALSE = DEVICE ONLY SENDS UPDATES WHEN VALUES CHANGE!
                // wiiStick.SetLEDs(false, true, false, false);

                // Enable processing of updates.

                infoUpdateTimer.Enabled = true;

                // Prevent connect being pressed more than once.

                button_Connect.Enabled = false;
                button_BluetoothAddDevice.Enabled = false;
                zeroout.Enabled = true;

                if (checkBox_AutoTare.Checked)
                    zeroout.PerformClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void wiiDevice_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            // Called every time there is a sensor update, values available using e.WiimoteState.
            // Use this for tracking and filtering rapid accelerometer and gyroscope sensor data.
            // The balance board values are basic, so can be accessed directly only when needed.

            // var props = e.GetType().GetProperties();
            // var sb = new StringBuilder();
            // foreach (var p in props)
            // {
            //     sb.AppendLine(p.Name + ": " + p.GetValue(e, null));
            // }
            // BalanceWalker.FormMain.consoleBoxWriteLine(sb.ToString());

        }

        private void wiiDevice_WiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs e)
        {
            // This is not needed for balance boards.
        }

        void infoUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Pass event onto the form GUI thread.

            this.BeginInvoke(new Action(() => InfoUpdate()));
        }

        private void InfoUpdate()
        {
            if (wiiBalanceBoard.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                label_Status.Text = "DEVICE IS NOT A BALANCE BOARD...";
                return;
            }

            // Get the current sensor KG values. (no temperature / latitude correction, can't set zero point properly.)

            var rwWeight = wiiBalanceBoard.WiimoteState.BalanceBoardState.WeightKg;

            var rwTopLeft = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesKg.TopLeft;
            var rwTopRight = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesKg.TopRight;
            var rwBottomLeft = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesKg.BottomLeft;
            var rwBottomRight = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesKg.BottomRight;
            var aButton = wiiBalanceBoard.WiimoteState.ButtonState.A;

            // The alternative .SensorValuesRaw is meaningless in terms of actual weight. not adjusted with 0KG, 17KG and 34KG calibration data.

            //var rwTopLeft     = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesRaw.TopLeft     - wiiBalanceBoard.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.TopLeft;
            //var rwTopRight    = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesRaw.TopRight    - wiiBalanceBoard.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.TopRight;
            //var rwBottomLeft  = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesRaw.BottomLeft  - wiiBalanceBoard.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.BottomLeft;
            //var rwBottomRight = wiiBalanceBoard.WiimoteState.BalanceBoardState.SensorValuesRaw.BottomRight - wiiBalanceBoard.WiimoteState.BalanceBoardState.CalibrationInfo.Kg0.BottomRight;

            // Show the sensor values in kg.
            label_rwWT.Text = rwWeight.ToString("0.0");
            label_rwTL.Text = rwTopLeft.ToString("0.0");
            label_rwTR.Text = rwTopRight.ToString("0.0");
            label_rwBL.Text = rwBottomLeft.ToString("0.0");
            label_rwBR.Text = rwBottomRight.ToString("0.0");

            // Prevent negative values by tracking lowest possible value and making it a zero based offset.

            if (rwWeight > 5)
            {
                button_SetCenterOffset.Enabled = true;
                button_SetCenterOffset.Text = "Set current balance as center";
                //toolTip1.SetToolTip(button_SetCenterOffset, "While standing or sitting on the balance board, click this button to set your current balance point as center");
            }
            else
            {
                if (resetCenterOffsetPossible)
                {
                    button_SetCenterOffset.Enabled = true;
                    button_SetCenterOffset.Text = "Reset Center Offset";
                    //toolTip1.SetToolTip(button_SetCenterOffset, "Revert back to the original center balance point, for the X/Y controlsl");
                }

                else
                {
                    button_SetCenterOffset.Text = "Set current balance as center";
                    button_SetCenterOffset.Enabled = false;
                }
            }

            if (rwTopLeft < naCorners) naCorners = rwTopLeft;
            if (rwTopRight < naCorners) naCorners = rwTopRight;
            if (rwBottomLeft < naCorners) naCorners = rwBottomLeft;
            if (rwBottomRight < naCorners) naCorners = rwBottomRight;

            // Negative total weight is reset to zero as jumping or lifting the board causes negative spikes, which would break 'in use' checks.

            var owWeight = rwWeight < 0f ? 0f : rwWeight;

            var owTopLeft = rwTopLeft -= naCorners;
            var owTopRight = rwTopRight -= naCorners;
            var owBottomLeft = rwBottomLeft -= naCorners;
            var owBottomRight = rwBottomRight -= naCorners;

            // Get offset that would make current values the center of mass.

            if (setCenterOffset)
            {
                setCenterOffset = false;
                resetCenterOffsetPossible = true;

                var rwHighest = Math.Max(Math.Max(rwTopLeft, rwTopRight), Math.Max(rwBottomLeft, rwBottomRight));

                oaTopLeft = rwHighest - rwTopLeft;
                oaTopRight = rwHighest - rwTopRight;
                oaBottomLeft = rwHighest - rwBottomLeft;
                oaBottomRight = rwHighest - rwBottomRight;
            }

            // Keep values only when board is being used, otherwise offsets and small value jitters can trigger unwanted actions.

            if (owWeight > 5f)  // minimal weight of 5kg
            {
                owTopLeft += oaTopLeft;
                owTopRight += oaTopRight;
                owBottomLeft += oaBottomLeft;
                owBottomRight += oaBottomRight;
            }
            else
            {
                owTopLeft = 0;
                owTopRight = 0;
                owBottomLeft = 0;
                owBottomRight = 0;
            }

            label_owWT.Text = owWeight.ToString("0.0");
            label_owTL.Text = owTopLeft.ToString("0.0") + "\r\n" + oaTopLeft.ToString("0.0");
            label_owTR.Text = owTopRight.ToString("0.0") + "\r\n" + oaTopRight.ToString("0.0");
            label_owBL.Text = owBottomLeft.ToString("0.0") + "\r\n" + oaBottomLeft.ToString("0.0");
            label_owBR.Text = owBottomRight.ToString("0.0") + "\r\n" + oaBottomRight.ToString("0.0");

            // Calculate each weight ratio.

            var owrPercentage = 100 / (owTopLeft + owTopRight + owBottomLeft + owBottomRight);
            var owrTopLeft = owrPercentage * owTopLeft;
            var owrTopRight = owrPercentage * owTopRight;
            var owrBottomLeft = owrPercentage * owBottomLeft;
            var owrBottomRight = owrPercentage * owBottomRight;

            label_owrTL.Text = owrTopLeft.ToString("0.0");
            label_owrTR.Text = owrTopRight.ToString("0.0");
            label_owrBL.Text = owrBottomLeft.ToString("0.0");
            label_owrBR.Text = owrBottomRight.ToString("0.0");

            // Calculate balance ratio.

            var brX = owrBottomRight + owrTopRight;
            var brY = owrBottomRight + owrBottomLeft;

            label_brX.Text = brX.ToString("0.0");
            label_brY.Text = brY.ToString("0.0");

            // Diagonal ratio used for turning on the spot.

            var brDL = owrPercentage * (owBottomLeft + owTopRight);
            var brDR = owrPercentage * (owBottomRight + owTopLeft);
            var brDF = Math.Abs(brDL - brDR);

            label_brDL.Text = brDL.ToString("0.0");
            label_brDR.Text = brDR.ToString("0.0");
            label_brDF.Text = brDF.ToString("0.0");

            // Convert sensor values into actions.

            var sendLeft = false;
            var sendRight = false;
            var sendForward = false;
            var sendBackward = false;
            var sendModifier = false;
            var sendJump = false;
            var sendDiagonalLeft = false;
            var sendDiagonalRight = false;

            if (brX < (float)(50 - numericUpDown_TLR.Value)) sendLeft = true;
            if (brX > (float)(50 + numericUpDown_TLR.Value)) sendRight = true;
            if (brY < (float)(50 - numericUpDown_TFB.Value)) sendForward = true;
            if (brY > (float)(50 + numericUpDown_TFB.Value)) sendBackward = true;

            if (brX < (float)(50 - numericUpDown_TMLR.Value)) sendModifier = true;
            else if (brX > (float)(50 + numericUpDown_TMLR.Value)) sendModifier = true;
            else if (brY < (float)(50 - numericUpDown_TMFB.Value)) sendModifier = true;
            else if (brY > (float)(50 + numericUpDown_TMFB.Value)) sendModifier = true;

            if (walkingOn)
            {
                // Up foot should be opposite to down foor
                Foot currentFoot = Foot.None;
                if (sendLeft) currentFoot = Foot.Left;
                if (sendRight) currentFoot = Foot.Right;

                DateTime now = DateTime.UtcNow;

                bool alternateFoot = (currentFoot != Foot.None && currentFoot == get_opposite_foot(lastFoot));
                if (alternateFoot) BalanceWalker.FormMain.consoleBoxWriteLine("Alternates");

                double seconsdSinceLastFootSwitch = (now - lastFootSwitchTime).TotalMilliseconds / 1000;
                double secondsSinceLastWalk = (now - lastWalkTime).TotalMilliseconds / 1000;

                if (sprintingOn)
                {
                    if (alternateFoot && !isSprinting && seconsdSinceLastFootSwitch < sprintStartTime)
                    {
                        actionList.Modifier.Start();
                        BalanceWalker.FormMain.consoleBoxWriteLine("They be sprinting");
                        isSprinting = true;
                    }
                    else if (isSprinting && seconsdSinceLastFootSwitch >= sprintEndTime && !isJumping)
                    {
                        BalanceWalker.FormMain.consoleBoxWriteLine("They be haulting (no sprinting)");
                        actionList.Modifier.Stop();
                        isSprinting = false;
                    }
                }

                if (alternateFoot && !isWalking && (seconsdSinceLastFootSwitch < walkStartTime || secondsSinceLastWalk < walkContinuationTime))
                {
                    actionList.Forward.Start();
                    BalanceWalker.FormMain.consoleBoxWriteLine("They be walking");
                    if (secondsSinceLastWalk < walkContinuationTime) BalanceWalker.FormMain.consoleBoxWriteLine("They still be walking");
                    isWalking = true;
                }
                else if (isWalking && seconsdSinceLastFootSwitch >= walkEndTime && !isJumping)
                {
                    actionList.Forward.Stop();
                    BalanceWalker.FormMain.consoleBoxWriteLine("They be stopped");
                    lastWalkTime = now;
                    isWalking = false;
                }

                if (alternateFoot) lastFootSwitchTime = now;
                if (currentFoot != lastFoot) lastFootChangeStateTime = now;
                if (currentFoot != Foot.None) lastFoot = currentFoot;
            }

            if (turningOn)
            {
                brX = !float.IsNaN(brX) ? brX : 50;
                horizontalCenteringQueue.Enqueue(brX);
                horizontalCenteringAverage += brX;
                if (horizontalCenteringQueue.Count > maxAverageCount)
                {
                    horizontalCenteringAverage -= horizontalCenteringQueue.Dequeue();
                }


                int turnPercentage = 0;
                if (isWalking || isSprinting)
                {
                    turnPercentage = turning_movement_scale(horizontalCenteringAverage / horizontalCenteringQueue.Count, tiltMaxMoving, tiltSpeedMoving, turningNullZonePercentageMoving);
                }
                else
                {
                    turnPercentage = turning_movement_scale(brX, tiltMax, tiltSpeed, turningNullZonePercentage);
                }
                actionList.Left.changeAmount(turnPercentage);
                //TODO run only once
                actionList.Left.Start();
            }

            if (turningVerticalOn)
            {
                brY = !float.IsNaN(brY) ? brY : 50;
                verticalCenteringQueue.Enqueue(brY);
                verticalCenteringAverage += brY;
                if (verticalCenteringQueue.Count > maxAverageCountVertical)
                {
                    verticalCenteringAverage -= verticalCenteringQueue.Dequeue();
                }


                int turnPercentage = 0;
                if (isWalking || isSprinting)
                {
                    turnPercentage = turning_movement_scale(verticalCenteringAverage / verticalCenteringQueue.Count, tiltMaxVerticalMoving, tiltSpeedVerticalMoving, turningNullZonePercentageVerticalMoving);
                }
                else
                {
                    turnPercentage = turning_movement_scale(brY, tiltMaxVertical, tiltSpeedVertical, turningNullZonePercentageVertical);
                }
                actionList.Backward.changeAmount(turnPercentage);
                //TODO run only once
                actionList.Backward.Start();
            }

            if (jumpingOn)
            {
                DateTime now = DateTime.UtcNow;
                bool offBalanceBoard = owWeight < 1f;
                double secondsSinceLastJump = (now - lastGroundTime).TotalMilliseconds / 1000;

                // Jump
                if (!isJumping && wasOnBalanceBoardLastTick && offBalanceBoard)
                {
                    actionList.Jump.Start();
                    BalanceWalker.FormMain.consoleBoxWriteLine("They be jumping");
                    isJumping = true;
                }
                // Flying
                if (isJumping && secondsSinceLastJump >= maxJumpTime)
                {
                    actionList.Jump.Stop();
                    isJumping = false;
                    BalanceWalker.FormMain.consoleBoxWriteLine("They be flying (how you do that?)");
                }
                else if (isJumping && !offBalanceBoard)
                {
                    actionList.Jump.Stop();
                    BalanceWalker.FormMain.consoleBoxWriteLine("They be landing");
                    isJumping = false;
                }

                if (!offBalanceBoard) lastGroundTime = now;

                wasOnBalanceBoardLastTick = !offBalanceBoard;

            }



            // Display actions.

            label_Status.Text = "Result: ";

            if (sendForward) label_Status.Text += "Forward";
            if (sendLeft) label_Status.Text += "Left";
            if (sendBackward) label_Status.Text += "Backward";
            if (sendRight) label_Status.Text += "Right";
            if (sendModifier) label_Status.Text += " + Modifier";
            if (sendJump) label_Status.Text += "Jump";
            if (sendDiagonalLeft) label_Status.Text += "Diagonal Left";
            if (sendDiagonalRight) label_Status.Text += "Diagonal Right";
            if (isWalking) label_Status.Text += " & Walking";
            if (isSprinting) label_Status.Text += " & Sprinting";
            if (isJumping) label_Status.Text += " & Jumping";
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop updates.

            infoUpdateTimer.Enabled = false;
            wiiBalanceBoard.Disconnect();

            // Prevent 'stuck' down keys from closing while doing an action.

            actionList.Left.Stop();
            actionList.Right.Stop();
            actionList.Forward.Stop();
            actionList.Backward.Stop();
            actionList.Modifier.Stop();
            actionList.Jump.Stop();
            actionList.DiagonalLeft.Stop();
            actionList.DiagonalRight.Stop();
        }

        private void zeroout_Click(object sender, EventArgs e)
        {
            wiiBalanceBoard.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
            naCorners = 0f;
            oaTopLeft = 0f;
            oaTopRight = 0f;
            oaBottomLeft = 0f;
            oaBottomRight = 0f;
        }

        public void consoleBoxWriteLine(string line)
        {
            consoleBox.AppendText(line);
            consoleBox.AppendText(Environment.NewLine);
        }

        private void checkBox_DisableActions_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.DisableActions = isChecked;
            Properties.Settings.Default.Save();

            bool status;
            if (checkBox_DisableActions.Checked)
                status = false;
            else
                status = true;

            label_ActionLeft.Enabled = status;
            label_ActionRight.Enabled = status;
            label_ActionForward.Enabled = status;
            label_ActionBackward.Enabled = status;
            label_ActionModifier.Enabled = status;
            label_ActionJump.Enabled = status;
            label_ActionDiagonalLeft.Enabled = status;
            label_ActionDiagonalRight.Enabled = status;
            label_TLR.Enabled = status;
            label_TFB.Enabled = status;
            label_TMLR.Enabled = status;
            label_TMFB.Enabled = status;
        }

        private void ShowValues_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.ShowValuesInConsole = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_SendCGtoXY_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.SendCGtoXY = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_Send4LoadSensors_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.Send4LoadSensors = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_StartupAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.StartupAutoConnect = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_AutoTare_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.AutoTare = isChecked;
            Properties.Settings.Default.Save();
        }

        private void checkBox_StartMinimized_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = ((CheckBox)sender).Checked;
            Properties.Settings.Default.StartMinimized = isChecked;
            Properties.Settings.Default.Save();
        }

        private static Foot get_opposite_foot(Foot f)
        {
            if (f == Foot.Left) return Foot.Right;
            if (f == Foot.Right) return Foot.Left;
            return Foot.None;
        }

        private static int turning_movement_scale(double tilt, double max, double speed, double deadZone)
        {
            if (50.0 + deadZone < tilt)
            {
                return (int)(max * 2.0 * (1.0 / (1 + Math.Pow(speed, (50.0 - tilt + deadZone))) - 0.5));
            }
            else if (50.0 - deadZone > tilt)
            {
                return (int)(max * 2.0 * (1.0 / (1.0 + Math.Pow(speed, (50.0 - tilt - deadZone))) - 0.5));
            }
            else
            {
                return 0;
            }
        }
    }
}
