using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GHI.Glide;
using Microsoft.SPOT.Hardware;
using GHI.Glide.Geom;
using GHI.Processor;
using Gadgeteer.Modules.GHIElectronics;

namespace SpaMonitor
{
    public partial class Program
    {
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");
            setup();
        }
        static bool IsEmpty = true;
        static GHI.Glide.Display.Window MainWindow;
        static GHI.Glide.UI.Image img;
        static GHI.Glide.UI.Button btn;
        static GHI.Glide.UI.TextBlock txt;
        static Bitmap picAvail, picNotAvail; 
        void setup()
        {
            //7" Displays
            Display.Width = 800;
            Display.Height = 480;
            Display.OutputEnableIsFixed = false;
            Display.OutputEnablePolarity = true;
            Display.PixelPolarity = false;
            Display.PixelClockRateKHz = 30000;
            Display.HorizontalSyncPolarity = false;
            Display.HorizontalSyncPulseWidth = 48;
            Display.HorizontalBackPorch = 88;
            Display.HorizontalFrontPorch = 40;
            Display.VerticalSyncPolarity = false;
            Display.VerticalSyncPulseWidth = 3;
            Display.VerticalBackPorch = 32;
            Display.VerticalFrontPorch = 13;
            Display.Type = Display.DisplayType.Lcd;
            if (Display.Save())      // Reboot required?
            {
                PowerState.RebootDevice(false);
            }
            CapacitiveTouchController.Initialize(GHI.Pins.FEZCobraII.Socket4.Pin3);
            GlideTouch.Initialize();

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.Form1));
            img = (GHI.Glide.UI.Image)MainWindow.GetChildByName("img");
            btn = (GHI.Glide.UI.Button)MainWindow.GetChildByName("BtnChange");
            txt = (GHI.Glide.UI.TextBlock)MainWindow.GetChildByName("txtStatus");
            GT.Picture pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.empty), GT.Picture.PictureEncoding.JPEG);
            img.Bitmap = pic.MakeBitmap();
            Glide.MainWindow = MainWindow;
            btn.ReleaseEvent += btn_ReleaseEvent; 
            Glide.FitToScreen = true;
            //Thread th1 = new Thread(new ThreadStart(LoopButton));
            //th1.Start();
        }

        void btn_ReleaseEvent(object sender)
        {
            changeState();
        }

        void LoopButton()
        {
            while (true)
            {
                if (!Mainboard.LDR0.Read())
                {
                    changeState();
                }
                Thread.Sleep(200);
            }
        }
        void changeState()
        {
            Bitmap bmp;
            IsEmpty = !IsEmpty;
            if (IsEmpty)
            {
                if (picAvail == null)
                {
                    var temp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.empty), GT.Picture.PictureEncoding.JPEG);
                    picAvail = temp.MakeBitmap();
                }
                bmp = picAvail;
                txt.Text = "Available";
            }
            else
            {
                if (picNotAvail == null)
                {
                    var temp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.full), GT.Picture.PictureEncoding.JPEG);
                    picNotAvail = temp.MakeBitmap();
                }
                bmp = picNotAvail;
                txt.Text = "Being Used";
            }
            img.Bitmap = bmp;
            img.Invalidate();
            txt.Invalidate();
          
        }

        void btn_TapEvent(object sender)
        {
            changeState();
        }
    }
    //driver for touch screen
    public class CapacitiveTouchController
    {
        private InterruptPort touchInterrupt;
        private I2CDevice i2cBus;
        private I2CDevice.I2CTransaction[] transactions;
        private byte[] addressBuffer;
        private byte[] resultBuffer;

        private static CapacitiveTouchController _this;

        public static void Initialize(Cpu.Pin PortId)
        {
            if (_this == null)
                _this = new CapacitiveTouchController(PortId);
        }

        private CapacitiveTouchController()
        {
        }

        private CapacitiveTouchController(Cpu.Pin portId)
        {
            transactions = new I2CDevice.I2CTransaction[2];
            resultBuffer = new byte[1];
            addressBuffer = new byte[1];
            i2cBus = new I2CDevice(new I2CDevice.Configuration(0x38, 400));
            touchInterrupt = new InterruptPort(portId, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeBoth);
            touchInterrupt.OnInterrupt += (a, b, c) => this.OnTouchEvent();
        }

        private void OnTouchEvent()
        {
            for (var i = 0; i < 5; i++)
            {
                var first = this.ReadRegister((byte)(3 + i * 6));
                var x = ((first & 0x0F) << 8) + this.ReadRegister((byte)(4 + i * 6));
                var y = ((this.ReadRegister((byte)(5 + i * 6)) & 0x0F) << 8) + this.ReadRegister((byte)(6 + i * 6));

                if (x == 4095 && y == 4095)
                    break;

                if (((first & 0xC0) >> 6) == 1)
                    GlideTouch.RaiseTouchUpEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
                else
                    GlideTouch.RaiseTouchDownEvent(null, new GHI.Glide.TouchEventArgs(new Point(x, y)));
            }
        }

        private byte ReadRegister(byte address)
        {
            this.addressBuffer[0] = address;

            this.transactions[0] = I2CDevice.CreateWriteTransaction(this.addressBuffer);
            this.transactions[1] = I2CDevice.CreateReadTransaction(this.resultBuffer);

            this.i2cBus.Execute(this.transactions, 1000);

            return this.resultBuffer[0];
        }
    }
}
