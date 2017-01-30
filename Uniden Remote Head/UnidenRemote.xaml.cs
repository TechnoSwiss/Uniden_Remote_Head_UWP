using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Text;
using Windows.Storage;
using Windows.UI.Core;
using Windows.System.Display;
using Windows.Devices.Power;

using System.Xml;
using System.Xml.Serialization;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using System.Text.RegularExpressions;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Uniden_Remote_Head
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public class Settings
        {
            public Communication Communication = new Communication();
            public Scanner Scanner = new Scanner();
            public Theme Theme = new Theme();
        }

        public class Communication
        {
            public string deviceID = "BOLUTEK";
            public string comPort = "COM1";
            public int baudRate = 1152000;
        }

        public class Scanner
        {
            public int volume = 5;
            public int squelch = 14;
            public Boolean mute = false;
            public int backlight = 0;
        }

        public class Theme
        {
            public int foregroundRed = 0;
            public int foregroundGreen = 255;
            public int foregroundBlue = 191;
        }

        private Object thisLock = new Object();

        private Settings scannerSettings = new Settings();

        private StreamSocket _socket = null;

        private RfcommDeviceService _service;

        DeviceInformation device = null;

        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        private DisplayRequest displayRequest;
        private int drCounter = 0;

        private Boolean receiveData = false;
        private int receiveDataThreads = 0;

        private Boolean connected = false;
        private Boolean connectionChange = false;

        private Boolean cancelFuncClick = false;

        const int failedReadMax = 3;
        private int failedRead = failedReadMax;
        private Boolean scannerStateKnown = false;

        private int backLight = -1;

        private Boolean UpdateSettings = false;
        private Boolean UpdateTheme = false;

        public MainPage()
        {
            this.InitializeComponent();

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                ApplicationView view = ApplicationView.GetForCurrentView();
                view.TryEnterFullScreenMode();
                view.FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;
            }

            Windows.Storage.ApplicationDataCompositeValue compositeCommunication = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["communicationSettings"];
            if (compositeCommunication == null)
            {
                compositeCommunication = new Windows.Storage.ApplicationDataCompositeValue();
                compositeCommunication["deviceID"] = scannerSettings.Communication.deviceID;
                compositeCommunication["comPort"] = scannerSettings.Communication.comPort;
                compositeCommunication["baudRate"] = scannerSettings.Communication.baudRate;

                localSettings.Values["communicationSettings"] = compositeCommunication;
            }
            else
            {
                scannerSettings.Communication.deviceID = (String)compositeCommunication["deviceID"];
                scannerSettings.Communication.comPort = (String)compositeCommunication["comPort"];
                scannerSettings.Communication.baudRate = (int)compositeCommunication["baudRate"];
            }

            Windows.Storage.ApplicationDataCompositeValue compositeScanner = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["scannerSettings"];
            if (compositeScanner == null)
            {
                compositeScanner = new Windows.Storage.ApplicationDataCompositeValue();
                compositeScanner["volume"] = scannerSettings.Scanner.volume;
                compositeScanner["squelch"] = scannerSettings.Scanner.squelch;
                compositeScanner["mute"] = scannerSettings.Scanner.mute;
                compositeScanner["backlight"] = scannerSettings.Scanner.backlight;

                localSettings.Values["scannerSettings"] = compositeScanner;
            }
            else
            {
                scannerSettings.Scanner.volume = (int)compositeScanner["volume"];
                scannerSettings.Scanner.squelch = (int)compositeScanner["squelch"];
                scannerSettings.Scanner.mute = (Boolean)compositeScanner["mute"];
                scannerSettings.Scanner.backlight = (int)compositeScanner["backlight"];
            }

            Windows.Storage.ApplicationDataCompositeValue compositeTheme = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["themeSettings"];
            if (compositeTheme == null)
            {
                compositeTheme = new Windows.Storage.ApplicationDataCompositeValue();
                compositeTheme["foregroundRed"] = scannerSettings.Theme.foregroundRed;
                compositeTheme["foregroundGreen"] = scannerSettings.Theme.foregroundGreen;
                compositeTheme["foregroundBlue"] = scannerSettings.Theme.foregroundBlue;

                localSettings.Values["themeSettings"] = compositeTheme;
            }
            else
            {
                scannerSettings.Theme.foregroundRed = (int)compositeTheme["foregroundRed"];
                scannerSettings.Theme.foregroundGreen = (int)compositeTheme["foregroundGreen"];
                scannerSettings.Theme.foregroundBlue = (int)compositeTheme["foregroundBlue"];
            }
            sldrRed.Value = scannerSettings.Theme.foregroundRed;
            sldrGreen.Value = scannerSettings.Theme.foregroundGreen;
            sldrBlue.Value = scannerSettings.Theme.foregroundBlue;
            UpdateTheme = true;

            startTimers();
        }

        private void UpdateSettingsCommunications()
        {
            Windows.Storage.ApplicationDataCompositeValue compositeCommunication = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["communicationSettings"];

            compositeCommunication["deviceID"] = scannerSettings.Communication.deviceID;
            compositeCommunication["comPort"] = scannerSettings.Communication.comPort;
            compositeCommunication["baudRate"] = scannerSettings.Communication.baudRate;

            localSettings.Values["communicationSettings"] = compositeCommunication;
        }

        private void UpdateSettingsScanner()
        {
            Windows.Storage.ApplicationDataCompositeValue compositeScanner = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["scannerSettings"];

            compositeScanner["volume"] = scannerSettings.Scanner.volume;
            compositeScanner["squelch"] = scannerSettings.Scanner.squelch;
            compositeScanner["mute"] = scannerSettings.Scanner.mute;
            compositeScanner["backlight"] = scannerSettings.Scanner.backlight;

            localSettings.Values["scannerSettings"] = compositeScanner;
        }

        private void UpdateSettingsTheme()
        {
            Windows.Storage.ApplicationDataCompositeValue compositeTheme = (Windows.Storage.ApplicationDataCompositeValue)localSettings.Values["themeSettings"];

            compositeTheme["foregroundRed"] = scannerSettings.Theme.foregroundRed;
            compositeTheme["foregroundGreen"] = scannerSettings.Theme.foregroundGreen;
            compositeTheme["foregroundBlue"] = scannerSettings.Theme.foregroundBlue;

            localSettings.Values["themeSettings"] = compositeTheme;
        }

        private void btnHamburger_Click(object sender, RoutedEventArgs e)
        {
            mySplitView.IsPaneOpen = !mySplitView.IsPaneOpen;
        }

        private void btnVolUp_Click(object sender, RoutedEventArgs e)
        {
            if (scannerSettings.Scanner.volume < 30)
            {
                scannerSettings.Scanner.volume += 1;
                UpdateSettings = true;
            }
            SendNow("VOL," + scannerSettings.Scanner.volume.ToString());
        }

        private void btnVolDown_Click(object sender, RoutedEventArgs e)
        {
            if (scannerSettings.Scanner.volume > 0)
            {
                scannerSettings.Scanner.volume -= 1;
                UpdateSettings = true;
            }
            SendNow("VOL," + scannerSettings.Scanner.volume.ToString());
        }

        private void btnMute_Click(object sender, RoutedEventArgs e)
        {
            if (scannerSettings.Scanner.mute) // button has been clicked, if muted we'll unmute, else mute
            {
                SendNow("VOL," + scannerSettings.Scanner.volume.ToString());
                scannerSettings.Scanner.mute = false;
                UpdateSettings = true;
            }
            else
            {
                SendNow("VOL,0");
                scannerSettings.Scanner.mute = true;
                UpdateSettings = true;
            }
        }

        private void btnSquelchUp_Click(object sender, RoutedEventArgs e)
        {
            if (scannerSettings.Scanner.squelch < 19)
            {
                scannerSettings.Scanner.squelch += 1;
                UpdateSettings = true;
            }
            SendNow("SQL," + scannerSettings.Scanner.squelch.ToString());
        }

        private void btnSquelchDown_Click(object sender, RoutedEventArgs e)
        {
            if (scannerSettings.Scanner.squelch > 0)
            {
                scannerSettings.Scanner.squelch -= 1;
                UpdateSettings = true;
            }
            SendNow("SQL," + scannerSettings.Scanner.squelch.ToString());
        }

        private void btnCloseCall_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,Q,P");
        }

        private void btnBacklight_Click(object sender, RoutedEventArgs e)
        {
            scannerSettings.Scanner.backlight++;
            if (scannerSettings.Scanner.backlight > 3)
                scannerSettings.Scanner.backlight = 0;
            SendNow("KEY,V,P");
            UpdateSettings = true;
        }

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,>,P");
        }

        private void btnFunction_Click(object sender, RoutedEventArgs e)
        {
            if(!cancelFuncClick)
            {
                SendNow("KEY,F,P");
            }
            cancelFuncClick = false;
        }

        private void btnFunction_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (!cancelFuncClick)
            {
                cancelFuncClick = true;
                SendNow("KEY,F,L");
            }
        }

        private void btnFunction_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (!cancelFuncClick)
            {
                cancelFuncClick = true;
                SendNow("KEY,F,L");
            }
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,<,P");
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,S,P");
        }

        private void btnHold_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,H,P");
        }

        private void btnPol_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,P,P");
        }

        private void btnHP_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,W,P");
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,M,P");
        }

        private void btnLockOut_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,L,P");
        }

        private void btnOne_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,1,P");
        }

        private void btnTwo_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,2,P");
        }

        private void btnThree_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,3,P");
        }

        private void btnFour_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,4,P");
        }

        private void btnFive_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,5,P");
        }

        private void btnSix_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,6,P");
        }

        private void btnSeven_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,7,P");
        }

        private void btnEight_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,8,P");
        }

        private void btnNine_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,9,P");
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,.,P");
        }

        private void btnZero_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,0,P");
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            SendNow("KEY,E,P");
        }

        private async void SendNow(string msg)
        {
            DataWriter writer = null;

            try
            {
                if (!connected) { return; }
                writer = new DataWriter(_socket.OutputStream);

                writer.WriteString(msg + "\r");

                // Launch an async task to 
                //complete the write operation
                await writer.StoreAsync().AsTask();

                writer.DetachStream();
                writer.Dispose();
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    tbError.Text = ex.Message;
                });
                if (writer != null)
                {
                    writer.DetachStream();
                }
            }
        }

        private async Task<uint> Send(string msg)
        {
            tbError.Text = string.Empty;

            DataWriter writer = null;

            try
            {
                if (!connected) { return 0; }

                writer = new DataWriter(_socket.OutputStream);

                writer.WriteString(msg + "\r");

                // Launch an async task to 
                //complete the write operation
                var store = writer.StoreAsync().AsTask();

                writer.DetachStream();
                writer.Dispose(); // does this fix the memory leak?

                return await store;
            }
            catch (Exception ex)
            {
                tbError.Text = ex.Message;

                if (writer != null)
                {
                    writer.DetachStream();
                }

                return 0;
            }
        }

        private async void Receive()
        {
            // we want to make sure only 1 receive thread is running
            if(receiveDataThreads > 0)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    tbError.Text = "There appears to already be a Receive thread running";
                });
                return;
            } else
            {
                receiveDataThreads++;
            }

            DataReader reader = new DataReader(_socket.InputStream);
            string tempString = string.Empty;
            Boolean clearString = true;

            do
            {
                try
                {
                    reader.InputStreamOptions = InputStreamOptions.Partial;
                    await reader.LoadAsync(100);
                    do
                    {
                        byte[] readByte = new byte[reader.UnconsumedBufferLength];
                        //char[] readUTF16 = new char[readByte.Length];
                        List<char> readUTF16 = new List<char>();
                        reader.ReadBytes(readByte);

                        failedRead = 0;

                        for (int loop = 0; loop < readByte.Length; loop++)
                        {
                            // File Format has been a very helpful site for figuring out all the Unicode codes
                            // http://www.fileformat.info/info/charset/UTF-16/list.htm
                            // http://www.fileformat.info/info/unicode/category/So/list.htm

                            switch (readByte[loop])
                            {
                                // Scan Up
                                case 0x81:
                                    readUTF16.Add('↑');
                                    break;

                                // Scan Down
                                case 0x82:
                                    readUTF16.Add('↓');
                                    break;

                                // Close Call Priority
                                case 0x87:
                                    readUTF16.Add(' ');  // Close Call Priority First Line
                                    break;
                                case 0x88:
                                    readUTF16.Add(' ');  // Close Call Priority First Line
                                    break;
                                case 0x89:
                                    readUTF16.Add('\u24B8');  // Close Call Priority Second Line
                                    break;
                                case 0x8a:
                                    readUTF16.Add('\u25CE');  // Close Call Priority Second Line
                                    break;

                                // Function
                                case 0x8b:
                                    readUTF16.Add('\uD83C'); // 🅵
                                    readUTF16.Add('\uDD75');
                                    break;

                                // HOLD
                                case 0x8d:
                                    readUTF16.Add('H');
                                    break;
                                case 0x8e:
                                    readUTF16.Add('O');
                                    break;
                                case 0x8f:
                                    readUTF16.Add('L');
                                    break;
                                case 0x90:
                                    readUTF16.Add('D');
                                    break;

                                // Lock Out
                                case 0x95:
                                    readUTF16.Add('L');
                                    break;
                                case 0x96:
                                    readUTF16.Add('/');
                                    break;
                                case 0x97:
                                    readUTF16.Add('O');
                                    break;

                                // AM NFM WFM FM FMB (Modulation)
                                case 0x99:
                                    readUTF16.Add('A');
                                    break;
                                case 0x9a:
                                    readUTF16.Add('M');
                                    break;
                                case 0x9b:
                                    break;
                                case 0x9c:
                                    readUTF16.Add('F');
                                    break;
                                case 0x9d:
                                    break;
                                case 0x9e:
                                    readUTF16.Add('N');
                                    break;
                                case 0x9f:
                                    readUTF16.Add('W');
                                    break;

                                // Priority
                                case 0xa1:
                                    readUTF16.Add('P');
                                    break;
                                case 0xa2:
                                    readUTF16.Add('R');
                                    break;

                                // Attenuation
                                case 0xa3:
                                    readUTF16.Add('A');
                                    break;
                                case 0xa4:
                                    readUTF16.Add('T');
                                    break;
                                case 0xa5:
                                    readUTF16.Add('T');
                                    break;

                                // Priority 🅿

                                // Signal Strength 📶 ▂▃▅▆▇ 🌕🌔🌓🌒🌑
                                case 0xa6:
                                    readUTF16.Add('▂'); // 1 Bar
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    break;
                                case 0xa7:
                                    readUTF16.Add('▂'); // 2 Bars
                                    readUTF16.Add('▃');
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    break;
                                case 0xa8:
                                    readUTF16.Add('▂'); // 3 Bars 1st Char
                                    break;
                                case 0xa9:
                                    readUTF16.Add('▃'); // 3 Bars 2nd Char
                                    readUTF16.Add('▅');
                                    readUTF16.Add(' ');
                                    readUTF16.Add(' ');
                                    break;
                                case 0xaa:
                                    readUTF16.Add('▂'); // 4 Bars 1st Char
                                    break;
                                case 0xab:
                                    readUTF16.Add('▃'); // 4 Bars 2nd Char
                                    readUTF16.Add('▅');
                                    readUTF16.Add('▆');
                                    readUTF16.Add(' ');
                                    break;
                                case 0xac:
                                    readUTF16.Add('▂'); // 5 Bars 1st Char
                                    break;
                                case 0xad:
                                    readUTF16.Add('▃'); // 5 Bars 2nd Char
                                    readUTF16.Add('▅');
                                    readUTF16.Add('▆');
                                    readUTF16.Add('▇');
                                    break;

                                // Close Call DND
                                case 0xb5:
                                    readUTF16.Add(' '); // Close Call DND First Line
                                    break;
                                case 0xb6:
                                    readUTF16.Add(' '); // Close Call DND First Line
                                    break;
                                case 0xb7:
                                    readUTF16.Add('\u24B8'); // Close Call DND Secondary Line
                                    break;
                                case 0xb8:
                                    readUTF16.Add('\u25C9'); // Close Call DND Secondary Line
                                    break;

                                // AM NFM WFM FM FMB (Modulation)
                                case 0xb9:
                                    readUTF16.Add('M');
                                    break;
                                case 0xba:
                                    readUTF16.Add('B');
                                    break;

                                // Alert Mute
                                case 0xbb:
                                    readUTF16.Add('\uD83C'); // 🅼
                                    break;
                                case 0xbc:
                                    readUTF16.Add('\uDD7C');
                                    readUTF16.Add(' ');
                                    break;

                                // Cursor Left and Right
                                case 0xc4:
                                    readUTF16.Add('→');
                                    break;
                                case 0xc5:
                                    if (readByte[loop - 1] == 0xc4)
                                        readUTF16.Add(' ');
                                    else
                                        readUTF16.Add('I');
                                    break;
                                case 0xc6:
                                    if (readByte[loop - 2] == 0xc4)
                                        readUTF16.Add('←');
                                    else
                                        readUTF16.Add('F');
                                    break;
                                case 0xc7:
                                    if (readByte[loop - 3] == 0xc4)
                                        readUTF16.Add(' ');
                                    else
                                        readUTF16.Add('X');
                                    break;

                                // Scroll
                                case 0xc8:
                                    readUTF16.Add('S');
                                    break;
                                case 0xc9:
                                    readUTF16.Add('C');
                                    break;
                                case 0xca:
                                    readUTF16.Add('R');
                                    break;

                                default:
                                    readUTF16.Add((char)readByte[loop]);
                                    break;
                            }
                        }
                        tempString += new string(readUTF16.ToArray());

                        // if we have a carriage return then we should have a complete message
                        if (tempString.Contains("\r"))
                        {
                            string[] splitReceive = Regex.Split(tempString, @"(?<=[\r])");

                            foreach (string receivedLine in splitReceive)
                            {
                                if (!receivedLine.Contains("\r"))
                                {
                                    clearString = false;
                                    tempString = receivedLine;
                                    break;
                                }

                                if (receivedLine.IndexOf("STS") == 0)
                                {
                                    string[] splitString = receivedLine.Split(',');
                                    if(splitString.Count() < 2)
                                    {
                                        continue;
                                    }
                                    int numLines = splitString[1].Length;

                                    for (int lineNum = 0; lineNum < 6; lineNum++)
                                    {
                                        TextBlock textBox = (TextBlock) this.FindName("tbLine" + (lineNum + 1));
                                        if (numLines > lineNum)
                                        {
                                            textBox.Visibility = Visibility.Visible;
                                            textBox.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                            if (splitString[1][lineNum] == '0')
                                            {
                                                textBox.FontSize = 20;
                                                textBox.Height = 24;
                                            }
                                            else
                                            {
                                                textBox.FontSize = 40;
                                                textBox.Height = 48;
                                            }
                                        }
                                        else
                                        {
                                            textBox.Visibility = Visibility.Collapsed;
                                        }
                                    }

                                    for(int lineNum = 0; lineNum < numLines; lineNum++)
                                    {
                                        TextBlock textBox = (TextBlock)this.FindName("tbLine" + (lineNum + 1));
                                        if((2 + lineNum * 2) >= splitString.Length)
                                        {
                                            break;
                                        }
                                        textBox.Text = splitString[2 + lineNum * 2];
                                        if ((3 + lineNum * 2) >= splitString.Length)
                                        {
                                            break;
                                        }
                                        if (splitString[3 + lineNum * 2].IndexOf("*") == 0)
                                        {
                                            textBox.FontWeight = Windows.UI.Text.FontWeights.ExtraBlack;
                                        }
                                    }

                                    Int32.TryParse(splitString[splitString.Length - 1].TrimEnd('\r'), out backLight);
                                }
                            }
                            if(clearString)
                                tempString = String.Empty;
                            clearString = true;
                        }

                        if (receiveData)
                            await reader.LoadAsync(100);
                    } while (reader.UnconsumedBufferLength > 0);
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                    {
                        tbError.Text = ex.Message;
                        if(ex.Message.IndexOf("connection was aborted") != -1)
                        {
                            if (reader != null)
                            {
                                reader.DetachStream();
                                reader.Dispose();
                                receiveData = false;
                                reader = null;
                            }
                        }
                    });

                    tempString = String.Empty;

                    if (reader != null)
                    {
                        reader.DetachStream();
                    }
                }
            } while (receiveData);
            receiveDataThreads--;
        }

        private async void startTimers()
        {
            try
            {
                ThreadPoolTimer timer100ms = ThreadPoolTimer.CreatePeriodicTimer(async (t) =>
                {
                    DataWriter writer = null;

                    try
                    {
                        if (!connected && !connectionChange)
                        {
                            connectionChange = true;
                            device = await Connect();
                            if (connected)
                            {
                                receiveData = true;
                                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                                {
                                    Receive();
                                });
                            }
                            connectionChange = false;
                        }

                        if (!connected) { return; }

                        writer = new DataWriter(_socket.OutputStream);

                        writer.WriteString("STS\r");

                        // Launch an async task to 
                        //complete the write operation
                        await writer.StoreAsync();

                        writer.DetachStream();

                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            tbError.Text = ex.Message;
                        });
                        if (writer != null)
                        {
                            writer.DetachStream();
                        }
                    }
                }, TimeSpan.FromMilliseconds(100));

                ThreadPoolTimer timer1s = ThreadPoolTimer.CreatePeriodicTimer(async (t) =>
                {
                    try
                    {
                        if (UpdateSettings)
                        {
                            UpdateSettingsCommunications();
                            UpdateSettingsScanner();
                            UpdateSettingsTheme();
                            UpdateSettings = false;
                        }

                        if ((backLight >= 0) && backLight != scannerSettings.Scanner.backlight)
                        {
                            SendNow("KEY,V,P");
                        }

                        if (!scannerStateKnown && failedRead == 0)
                        {
                            SendNow("SQL," + scannerSettings.Scanner.squelch.ToString());
                            if (scannerSettings.Scanner.mute)
                            {
                                SendNow("VOL,0");
                            }
                            else
                            {
                                SendNow("VOL," + scannerSettings.Scanner.volume.ToString());
                            }
                            scannerStateKnown = true;
                        }

                        if (failedRead == failedReadMax)
                        {
                            scannerStateKnown = false;
                            backLight = -1;

                            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                            {
                                for (int lineNum = 0; lineNum < 6; lineNum++)
                                {
                                    TextBlock textBox = (TextBlock)this.FindName("tbLine" + (lineNum + 1));
                                    textBox.Visibility = Visibility.Visible;
                                    textBox.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                    textBox.FontSize = 30;
                                    textBox.Height = 32;
                                    if (lineNum != 2) textBox.Text = "";
                                }

                                string temp = "";
                                switch (tbLine3.Text.Length)
                                {
                                    case (15):
                                        temp = "Disconnected.";
                                        break;
                                    case (13):
                                        temp = "Disconnected..";
                                        break;
                                    case (14):
                                        temp = "Disconnected...";
                                        break;
                                    default:
                                        temp = "Disconnected.";
                                        break;
                                }
                                tbLine3.Text = temp;

                                if (connected & !connectionChange) { Disconnect(); }
                            });
                        }

                        if (failedRead < failedReadMax)
                        {
                            failedRead++;
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            tbError.Text = ex.Message;
                        });
                    }
                }, TimeSpan.FromMilliseconds(1000));

                ThreadPoolTimer timer5s = ThreadPoolTimer.CreatePeriodicTimer(async (t) =>
                {
                    try
                    {
                        // Create battery object
                        var battery = Battery.AggregateBattery;

                        // Get report
                        var report = battery.GetReport();

                        var percent = -1;

                        if (report.RemainingCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.HasValue)
                        {
                            percent = (int)((report.RemainingCapacityInMilliwattHours.Value /
                                (double)report.FullChargeCapacityInMilliwattHours.Value) * 100);
                        }

                        // Update UI
                        await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            tbBatStatus.Text = "Battery Status: " + report.Status.ToString();
                            tbBatPercent.Text = "Battery Percent: " + percent.ToString() + "%";
                            tbBatMaxCap.Text = "Battery Design Capacity: " + report.DesignCapacityInMilliwattHours.ToString() + "mWh";
                            tbBatCurCap.Text = "Battery Max Capacity: " + report.FullChargeCapacityInMilliwattHours.ToString() + "mWh";
                            tbBatRemainCap.Text = "Battery Remaining Capacity: " + report.RemainingCapacityInMilliwattHours.ToString() + "mWh";
                            tbBatChargeRate.Text = "Battery Charge Rate: " + report.ChargeRateInMilliwatts.ToString() + "mW";
                        });

                        await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            if (displayRequest == null)
                            {
                                displayRequest = new DisplayRequest();
                            }
                            // if battery is not present and we're still running, we have to assume that we're on AC power
                            // or if the charge rate is any non-negative number, we can assume we have a battery, and we're on AC power
                            if (report.Status == Windows.System.Power.BatteryStatus.NotPresent || report.ChargeRateInMilliwatts >= 0)
                            {
                                if (drCounter == 0)
                                {
                                    displayRequest.RequestActive();
                                    drCounter += 1;
                                }
                            }
                            else
                            {
                                while (drCounter > 0)
                                {
                                    displayRequest.RequestRelease();
                                    drCounter--;
                                }
                            }
                        });

                        if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                            {
                                ApplicationView view = ApplicationView.GetForCurrentView();
                                //if (!view.IsFullScreenMode)
                                {
                                    btnFunction.Focus(FocusState.Pointer);
                                    tbError.Text = view.IsFullScreen.ToString();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                        {
                            tbError.Text = ex.Message;
                        });
                    }
                }, TimeSpan.FromMilliseconds(5000));
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    tbError.Text = ex.Message;
                });
            }
        }

        private async Task<DeviceInformation> Connect()
        {
            DeviceInformation device = null;

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                tbError.Text = string.Empty;
            });

            try
            {
                var devices =
                      await DeviceInformation.FindAllAsync(
                        RfcommDeviceService.GetDeviceSelector(
                          RfcommServiceId.SerialPort));

                device = devices.Single(x => x.Name == scannerSettings.Communication.deviceID);

                _service = await RfcommDeviceService.FromIdAsync(device.Id);

                _socket = new StreamSocket();

                await _socket.ConnectAsync(
                      _service.ConnectionHostName,
                      _service.ConnectionServiceName,
                      SocketProtectionLevel.
                      BluetoothEncryptionAllowNullAuthentication);

                connected = true;
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    tbError.Text = ex.Message;
                });
                connected = false;
            }

            return device;
        }

        private void Disconnect()
        {
            tbError.Text = string.Empty;

            connectionChange = true;
            connected = false;
            receiveData = false;

            try
            {
                _socket.Dispose();
                _socket = null;
                _service.Dispose();
                _service = null;
            }
            catch (Exception ex)
            {
                tbError.Text = ex.Message;
            }
            connectionChange = false;
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            await Connect();
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            int dummy;

            if (!int.TryParse(tbInput.Text, out dummy))
            {
                tbError.Text = "Invalid input";
            }

            var noOfCharsSent = await Send(tbInput.Text);

            if (noOfCharsSent != 0)
            {
                tbError.Text = noOfCharsSent.ToString();
            }
        }

        private void btnReceive_Click(object sender, RoutedEventArgs e)
        {
            receiveData = true;
            Receive();
        }

        private void sldrChange(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                btnFunction.Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)sldrRed.Value, (byte)sldrGreen.Value, (byte)sldrBlue.Value));
                if (!UpdateTheme)
                {
                    return;
                }
                scannerSettings.Theme.foregroundRed = (int)sldrRed.Value;
                scannerSettings.Theme.foregroundGreen = (int)sldrGreen.Value;
                scannerSettings.Theme.foregroundBlue = (int)sldrBlue.Value;
                UpdateSettings = true;
            }
            catch (Exception ex)
            {
                tbError.Text = ex.Message;
            }
        }

    }
}
