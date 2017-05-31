using HidSharp;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;

namespace BuzzShow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Ellipse[] indicators = new Ellipse[20];
        bool[] buzzLights = new bool[4];
        bool[] choiceLights = new bool[4];
        bool[] emptyLights = new bool[4];
        bool endThreads = false;
        bool buzzerLockout = false;
        bool[] choiceLockouts = new bool[4];
        bool mode = false;
        int[] choices = new int[4];
        int buzzerPlayer = -1;
        bool buzzSoundActive;
        SoundPlayer buzzSound;
        bool choiceSoundActive;
        SoundPlayer choiceSound;
        bool blinking = false;
        int blinkLength = 200;
        public MainWindow()
        {
            InitializeComponent();
            indicators[0] = ind0;
            indicators[1] = ind1;
            indicators[2] = ind2;
            indicators[3] = ind3;
            indicators[4] = ind4;
            indicators[5] = ind5;
            indicators[6] = ind6;
            indicators[7] = ind7;
            indicators[8] = ind8;
            indicators[9] = ind9;
            indicators[10] = ind10;
            indicators[11] = ind11;
            indicators[12] = ind12;
            indicators[13] = ind13;
            indicators[14] = ind14;
            indicators[15] = ind15;
            indicators[16] = ind16;
            indicators[17] = ind17;
            indicators[18] = ind18;
            indicators[19] = ind19;
            
            //Load buzzer sound
            try
            {
                buzzSound = new SoundPlayer(@"buzz.wav");
                buzzSound.Load();
            }
            catch
            {
                buzzSoundBox.IsChecked = false;
                buzzSoundBox.IsEnabled = false;
                buzzSoundActive = false;
                buzzSoundBox.ToolTip = "Couldn't find sound file buzz.wav";
            }

            //Load lock-in sound
            try
            {
                choiceSound = new SoundPlayer(@"choice.wav");
                choiceSound.Load();
            }
            catch
            {
                lockInSoundBox.IsChecked = false;
                lockInSoundBox.IsEnabled = false;
                choiceSoundActive = false;
                lockInSoundBox.ToolTip = "Couldn't find sound file choice.wav";
            }

            Thread pollThread = new Thread(controllerPoller);
            Thread lightThread = new Thread(lightingThread);
            pollThread.Start();
            lightThread.Start();
        }


        public void controllerPoller()
        {
            var dInput = new DirectInput();
            var guid = Guid.Empty;

            foreach (var instance in dInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
            {
                var tempGuid = instance.InstanceGuid;
                var tempJoystick = new Joystick(dInput, tempGuid);
                tempJoystick.Acquire();
                var vendorId = tempJoystick.Properties.VendorId;
                var productId = tempJoystick.Properties.ProductId;
                var productName = tempJoystick.Properties.ProductName;
                Console.WriteLine("Found Gamepad: {0} VendorID: {1} ProductID: {2} GUID: {3}", productName, vendorId, productId, tempGuid);
                tempJoystick.Unacquire();
                if (vendorId == 0x054c)
                {
                    if (productId == 0x1000 || productId == 0x0002)
                    {
                        //Found the right device!
                        guid = tempGuid;
                    }
                }
            }

            if (guid == Guid.Empty)
            {
                Console.WriteLine("Didn't find a correct device");
                //Console.ReadKey();
                //Environment.Exit(1);
                //this.Close();
            }
            else
            {
                //Joystick found, now for the meat of the thread.

                var joystick = new Joystick(dInput, guid);
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();
                bool[] buttons = new bool[20];
                bool[] lastInputs = new bool[20];
                //bool threadRunning = true;

                while (!endThreads)
                {
                    joystick.Poll();
                    var data = joystick.GetBufferedData();
                    foreach (var state in data)
                    {

                        JoystickOffset offset = state.Offset;
                        if (offset.ToString().StartsWith("Buttons"))
                        {
                            StringBuilder builder = new StringBuilder();
                            builder.Append(offset);
                            builder.Replace("Buttons", "");
                            int chosenButton = int.Parse(builder.ToString());
                            if (state.Value == 0x0)
                            {
                                buttons[chosenButton] = false;
                            }
                            else
                            {
                                buttons[chosenButton] = true;
                            }
                        }

                    }

                    for (int i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i] != lastInputs[i])
                        {
                            lastInputs[i] = buttons[i];
                            if (buttons[i])
                            {
                                //A button was pushed. Now process it correctly.
                                if (i % 5 == 0)
                                {
                                    //buzzer was pushed. Are buzzers locked out?
                                    if (!buzzerLockout)
                                    {
                                        //No, take the lock, light up the buzzers, and light the indicator.
                                        buzzerLockout = true;
                                        buzzLights[i / 5] = true;
                                        buzzerPlayer = i / 5;
                                        try
                                        {
                                            this.Dispatcher.Invoke(new Action(delegate ()
                                            {
                                                indicators[i].Fill = Brushes.Red;
                                            }));
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e.ToString());
                                        }
                                        dispatchGet(false);
                                        if (buzzSoundActive)
                                        {
                                            buzzSound.Play();
                                        }
                                    }
                                }
                                else
                                {
                                    //a choice was pushed. Has this player already locked in?
                                    if (!choiceLockouts[i / 5])
                                    {
                                        //Nope, take the lock and light up the right choice
                                        choiceLockouts[i / 5] = true;

                                        choiceLights[i / 5] = true;

                                        Brush fillBrush = Brushes.White;
                                        if (i % 5 == 1)
                                        {
                                            fillBrush = Brushes.Goldenrod;
                                            choices[i / 5] = 3;
                                        }
                                        else if (i % 5 == 2)
                                        {
                                            fillBrush = Brushes.Green;
                                            choices[i / 5] = 2;
                                        }
                                        else if (i % 5 == 3)
                                        {
                                            fillBrush = Brushes.Orange;
                                            choices[i / 5] = 1;
                                        }
                                        else if (i % 5 == 4)
                                        {
                                            fillBrush = Brushes.Blue;
                                            choices[i / 5] = 0;
                                        }
                                        try
                                        {
                                            this.Dispatcher.Invoke(new Action(delegate ()
                                            {
                                                indicators[i].Fill = fillBrush;
                                            }));
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e.ToString());
                                        }
                                        if (choiceSoundActive)
                                        {
                                            choiceSound.Play();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Thread.Sleep(1);
                }
            }
        }

        public void lightingThread()
        {
            bool[] lastValues = new bool[4];
            bool changed = false;

            HidDeviceLoader loader = new HidDeviceLoader();
            HidDevice device = null;
            try
            {
                device = loader.GetDevices(0x054c, 0x1000).First();
            }
            catch
            {
                try
                {
                    device = loader.GetDevices(0x054c, 0x0002).First();
                }
                catch
                {
                    System.Windows.MessageBox.Show("No Buzz! controllers found. Are they plugged in?", "BuzzShow Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Dispatcher.Invoke(new Action (delegate () {
                        this.Close();
                    }));
                    Environment.Exit(1);
                }
            }
            HidStream stream;
            device.TryOpen(out stream);

            int blinkCountdown = 200;
            bool blinkOff = false;

            while (!endThreads)
            {
                bool[] lights;
                if (!mode)
                {
                    lights = buzzLights;
                }
                else lights = choiceLights;

                if (blinking)
                {
                    blinkCountdown--;
                    if (blinkCountdown == 0)
                    {
                        blinkCountdown = blinkLength;
                        blinkOff = !blinkOff;
                    }
                    if (blinkOff)
                    {
                        lights = emptyLights;
                    }
                }
                else blinkOff = false;
                //Console.WriteLine(lights[0].ToString() + " "+lights[1].ToString()+" " + lights[2].ToString() + " " + lights[3].ToString());
                for (int i = 0; i < lastValues.Length; i++)
                {
                    if (lights[i] != lastValues[i])
                    {
                        lastValues[i] = lights[i];
                        changed = true;
                    }
                }
                if (changed)
                {
                    byte[] message = new byte[6];
                    message[0] = 0x0;
                    message[1] = 0x0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (lights[i] == false) message[i + 2] = 0x0;
                        else message[i + 2] = 0xff;
                    }

                    //Message created, send it to the device

                    stream.Write(message);
                }
                changed = false;
                Thread.Sleep(1);
            }
        }

        public void dispatchGet(bool choiceMode)
        {
            Thread getThread = new Thread(new ThreadStart(() => sendGET(choiceMode)));
            getThread.Start();
        }
        public void sendGET(bool choiceMode)
        {
            StringBuilder urlBuilder = new StringBuilder();
            urlBuilder.Append("http://localhost/buzzshow/");
            if (!choiceMode)
            {
                //buzzer mode
                urlBuilder.Append("buzz?id="+buzzerPlayer);
            }
            else
            {
                urlBuilder.Append("answer?a="+choices[0]);
                urlBuilder.Append("&b=" + choices[1]);
                urlBuilder.Append("&c=" + choices[2]);
                urlBuilder.Append("&d=" + choices[3]);
            }
            Console.WriteLine("Send GET: " + urlBuilder.ToString());
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlBuilder.ToString());
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                Console.WriteLine("GET Success");
            }
            catch (Exception e)
            {
                Console.WriteLine("GET Failed: "+e.ToString());
            }
            
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Console.WriteLine("Window closed");
            endThreads = true;
        }

        private void buzzMode_Checked(object sender, RoutedEventArgs e)
        {
            mode = false;
        }

        private void choiceMode_Checked(object sender, RoutedEventArgs e)
        {
            mode = true;
        }

        private void resetBuzzers_Click(object sender, RoutedEventArgs e)
        {
            for(int i=0; i<indicators.Length; i += 5)
            {
                indicators[i].Fill = Brushes.White;
            }
            for(int i=0; i<buzzLights.Length; i++)
            {
                buzzLights[i] = false;
            }
            buzzerPlayer = -1;
            buzzerLockout = false;
            dispatchGet(false);
        }

        private void resetChoices_Click(object sender, RoutedEventArgs e)
        {
            for(int i=0; i<indicators.Length; i++)
            {
                if (i % 5 != 0)
                {
                    indicators[i].Fill = Brushes.White;
                }
            }
            for(int i=0; i<choiceLights.Length; i++)
            {
                choiceLights[i] = false;
                choices[i] = -1;
                choiceLockouts[i] = false;
            }
            dispatchGet(true);
        }

        private void sendChoicesButton_Click(object sender, RoutedEventArgs e)
        {
            dispatchGet(true);
        }

        private void buzzSoundBox_Checked(object sender, RoutedEventArgs e)
        {
            buzzSoundActive = true;
        }

        private void buzzSoundBox_Unchecked(object sender, RoutedEventArgs e)
        {
            buzzSoundActive = false;
        }

        private void lockInSoundBox_Checked(object sender, RoutedEventArgs e)
        {
            choiceSoundActive = true;
        }

        private void lockInSoundBox_Unchecked(object sender, RoutedEventArgs e)
        {
            choiceSoundActive = false;
        }

        private void blinkCheck_Checked(object sender, RoutedEventArgs e)
        {
            blinking = true;
        }

        private void blinkCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            blinking = false;
        }

        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (blinkLengthBox.Value != null)
            {
                blinkLength = blinkLengthBox.Value.Value*10;
                Console.WriteLine("Blink Length: " + blinkLength);
            }
        }
    }
}
