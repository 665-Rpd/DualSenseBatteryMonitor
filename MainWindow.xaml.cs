using HidSharp;
using Microsoft.Win32;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DualSenseBatteryMonitor
{
    //DualSense Battery Monitor
    //By PixelIndieDev

    public partial class MainWindow : Window
    {
        //Create Timer
        private readonly DispatcherTimer updateTimer = new DispatcherTimer(DispatcherPriority.Background);
        //Create controller verables
        private controllerWidget[] controllerWidgets = new controllerWidget[4];
        private int LastControllerCount = 0;

        //Low battery threshold
        private const int lowBatteryThreshold = 25;

        //Used for sizing
        private double controllerWidgetHeight;
        private double controllerWidgetWidth;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();

            MakeWindowClickThroughAndNoActivate(); //Allow mouse to pass through and avoid focus

            //Set up periodic update
            updateTimer.Tick += new EventHandler(updateTimer_Tick);
            //Interval every 5 seconds
            updateTimer.Interval = new TimeSpan(0, 0, 5);

            //Freeze background gradient to save on memory
            gradient_background.Freeze();

            DoStart();

            //This function will make this application always run on Windows startup
            AddToStartup();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Get the working area of the primary screen (excluding taskbar)
            var workingArea = System.Windows.SystemParameters.WorkArea;

            //Position the window at the top-right corner
            this.Left = workingArea.Right - this.Width;
            //Set height with offset
            this.Top = workingArea.Top + (workingArea.Height / 20);
        }

        public async void DoStart()
        {
            for (int i = 0; i < 4; i++)
            {
                var controllerWidget = new controllerWidget(true, i, 0);

                //For first widget, get the height and width of the widget
                if (i == 0) 
                {
                    controllerWidgetHeight = controllerWidget.Height;
                    controllerWidgetWidth = controllerWidget.Width;

                    //Already do this, this way this will only be triggered ones
                    Width = (controllerWidget.Width + 40); // Padding
                }

                double height = controllerWidget.Height;
                controllerWidgets[i] = controllerWidget;
                var FrameI = new Frame();
                FrameI.Content = controllerWidget;
                flowLayout_controller.Items.Add(FrameI);
            }

            await CheckControllers(); //Initial controller scan
        }

        private async void updateTimer_Tick(object sender, EventArgs e)
        {
            await CheckControllers();
        }

        public async Task CheckControllers()
        {
            //Stop timer before await
            updateTimer.Stop();

            //Get the battery levels async
            var controllerBatterlevels = await GetDualSenseBatteryLevelsAsync();
            LastControllerCount = 0;

            bool someoneHasLowBattery = false;

            if (controllerBatterlevels.Count <= 0)
            {
                Refresh_NoControllers();
            }
            else
            {
                foreach (var controllerBattery in controllerBatterlevels)
                {
                    controllerWidgets[controllerBattery.Key].RefreshData((LastControllerCount+1), controllerBattery.Value.BatteryPercent, controllerBattery.Value.IsCharging);

                    //Check if this controller has low battery and no one else has already activated the bool trigger
                    if (controllerBattery.Value.BatteryPercent < lowBatteryThreshold && someoneHasLowBattery == false)
                    {
                        someoneHasLowBattery = true;
                    }

                    LastControllerCount++;
                }
                Refresh_FromControllerIndex(controllerBatterlevels.Count);
            }

            //Resize window size
            UpdateWindowSize(controllerBatterlevels.Count);

            //Update Window on screen or not
            //Window will be on screen when a battery is lower then a certain amount
            ShouldShowWindow(someoneHasLowBattery);

            //Start timer again
            updateTimer.Start();
        }

        private void Refresh_NoControllers()
        {
            for (int i = 0; i < 4; i++)
            {
              controllerWidgets[i].RefreshData(0, 0, false);
            }
        }

        private void Refresh_FromControllerIndex(int controllerAmount)
        {
            //Max 4 controllers
            while (LastControllerCount < 4)
            {
                controllerWidgets[LastControllerCount].RefreshData(controllerAmount, 0, false);
                LastControllerCount++;
            }
        }

        private void UpdateWindowSize(int amount_children)
        {
            //Set window height
            //Each child gets its height + 10 (spacing)
            Height = amount_children*(controllerWidgetHeight+10);
        }

        private void ShouldShowWindow(bool lowBattery)
        {
            //Someone has low battery
            if (lowBattery)
            {
                Show(); //Always visible when battery low
            } else //No one has low battery
            {
                Hide();
            }
        }

        private void MakeWindowClickThroughAndNoActivate()
        {
            var WindowInteropHelper = new WindowInteropHelper(this).Handle;

            //Get current extended style
            int extended_style = GetWindowLong(WindowInteropHelper, GWL_EXSTYLE);

            //WS_EX_TOOLWINDOW  -> hides from Alt-Tab
            //WS_EX_NOACTIVATE  -> window won’t activate / steal focus
            //WS_EX_TRANSPARENT -> click events pass through
            int new_extended_style = extended_style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;

            SetWindowLong(WindowInteropHelper, GWL_EXSTYLE, new_extended_style);
        }



        private static async Task<Dictionary<int, (int BatteryPercent, bool IsCharging)>> GetDualSenseBatteryLevelsAsync()
        {
            var result = new Dictionary<int, (int, bool)>();
            //Get the list of current HID devices
            var deviceList = DeviceList.Local;
            //Look for first Dualsense (1356 3302)
            var controllers = deviceList.GetHidDevices(1356, 3302); //Vendor/Product ID

            int index = 0;

            foreach (var controller in controllers)
            {
                byte[] inputBuffer = null; //Declare buffer outside for use in finally

                try
                {
                    //Rent a byte array from the shared pool (avoids repeated allocations)
                    inputBuffer = ArrayPool<byte>.Shared.Rent(controller.GetMaxInputReportLength());

                    using (var stream = controller.Open())
                    {
                        await Task.Yield(); // Add a small delay before reading data
                        int bytesRead = stream.Read(inputBuffer, 0, inputBuffer.Length);

                        //If data is received from the controller, process it
                        if (bytesRead > 0)
                        {
                            //Check length of data read , USB is 64 and Bluetooth is 78
                            if (bytesRead == 64)
                            {
                                //In USB mode battery info is at 53 and charging info is at 54
                                //In USB mode charging seems to be 8 if charging and 0 when not
                                result[index++] = (getBatteryPercentage(inputBuffer[53], inputBuffer[54]), inputBuffer[54] > 0);
                            }
                            //Bluetooth has length of 78 and 3 modes:
                            else if (bytesRead == 78)
                            {
                                //0x31 header - Full BT report
                                if (inputBuffer[0] == 0x31)
                                {
                                    //In Bluetooth mode 0x31 battery level is at 54 and charging info is at 55
                                    //In Bluetooth charging seems to be indicated by 16 if charging and 0 when not
                                    result[index++] = (getBatteryPercentage(inputBuffer[54], inputBuffer[55]), inputBuffer[55] > 0);
                                } //0x01 header may indicate two bluetooth modes - Basic or Minimal BT report
                                else if (inputBuffer[0] == 0x01)
                                {
                                    //Check for many 0's in the data (ignoring the first few bytes)
                                    int zeroCount = 0;
                                    for (int i = 1; i < inputBuffer.Length; i++)  // Start checking from byte 1 (after the 0x01 byte)
                                    {
                                        if (inputBuffer[i] == 0x00)
                                        {
                                            zeroCount++;
                                        }
                                    }
                                    //0x01 may send only a few bytes that are indicating button presses and nothing else, most importantly no battery or charging info
                                    //If most of data is empty then controller is in "Minimal Bluetooth"
                                    //This mode is usually only seen when bluetooth module or pc was restarted and "fresh connection" is made
                                    if (zeroCount > 70)
                                    {
                                        //Minimal Bluetooth mode (mostly empty), try waking up
                                        //Read "Magic Packet" to "wake" controller to 0x31 (Full Bluetooth mode)
                                        WaketofullBT(controller);
                                        result[index++] = (1111, false);
                                    } else
                                    {
                                        //Basic Bluetooth mode, try waking up as well
                                        WaketofullBT(controller);
                                        //Use unreachable batterylevel for "error" code
                                        result[index++] = (1111, false);
                                    }

                                }
                                else
                                {
                                    //Unknown Bluetooth report format
                                    //Use unreachable batterylevel for "error" code
                                    result[index++] = (1115, false);
                                }
                            }
                            else
                            {
                                //Unknown report length
                                //Use unreachable batterylevel for "error" code
                                result[index++] = (888, false); //Unknown format
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    #if DEBUG
                    Debug.WriteLine($"[Warning] Could not read controller: {ex.Message}");
                    #endif
                }
                finally
                {
                    //Always return the buffer to the pool, even on exception
                    if (inputBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(inputBuffer);
                    }
                }
            }

            return result;
        }

        //Dualsense sometimes (usually when bluetooth was restarted) sends only button presses and no other info , to trigger it into full info mode it is required to read 0x05 report from it.
        //AFAIK when controller recieves request to read from it 0x05 report it switches to full bluetooth functionality
        private static void WaketofullBT(HidDevice controller)
        {
            try
            {
                if (controller.TryOpen(out HidStream hidStream))
                {
                    using (hidStream)
                    {
                        byte[] buffer = new byte[controller.GetMaxFeatureReportLength()];
                        buffer[0] = 0x05;  // Report ID

                        hidStream.GetFeature(buffer);
                    }
                }
                else
                {
                    //Could not open device
                    #if DEBUG
                    Debug.WriteLine("Couldn't open device");
                    #endif
                }
            }
            catch (Exception ex)
            {
                //Catch exception wake
                #if DEBUG
                Debug.WriteLine($"Error: {ex.Message}");
                #endif
            }
        }

        private static int getBatteryPercentage(int battery0, int battery1)
        {
            //Last 4 bits mark battery level
            //Battery level is on a scale 0 (empty) - 8 (full)
            int batterynumber0to8 = (battery0 & 0x0F);
            //Sometimes battery can report status 9 when full
            //Make it 8
            if (batterynumber0to8 > 8)
            {
                batterynumber0to8 = 8;
            }

            //Calculate percentage
            return (batterynumber0to8 * 100) / 8; //return the value
        }

        //Will at this program to the auto startup programs
        private static void AddToStartup()
        {
            //Get .exe install location
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            //Get registry key location
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            //Check if the key is not NULL
            if (key != null)
            {
                object existingValue = key.GetValue("DualSenseBatteryMonitor");

                //When the existingvalue is NULL or the new and old value match
                if (existingValue == null ||  !string.Equals(existingValue.ToString(), exePath, StringComparison.OrdinalIgnoreCase))
                {
                    //Add self to auto start up
                    key.SetValue("DualSenseBatteryMonitor", "\"" + exePath + "\"");
                } //If the value is not NULL and the string match, then do nothing

            } //If key is null, do nothing
        }
    }
}