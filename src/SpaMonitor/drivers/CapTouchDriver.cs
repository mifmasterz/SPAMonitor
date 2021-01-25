using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation.Media;
using System.IO.Ports;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Touch;
using Microsoft.SPOT.Net;
using System.IO;
using GHI.Glide;
using GHI.Glide.Display;
using GHI.Glide.UI;
using GHI.IO;
using GHI.Processor;
using GHI.IO.Storage;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Text;

using GlideButton = GHI.Glide.UI.Button;
using GlideColors = GHI.Glide.Colors;
using System.Collections;

namespace BogTechFlush
{
    class CapTouchDriver
    {
        static public DisplayNhd5 display;
        static InterruptPort touchPin;
        static Microsoft.SPOT.Touch.TouchInput[] touches;
        //
        // LCD backlight
        //
        //static OutputPort LCDbacklight;
        static long msBacklightTime;        // Time in ms for backlight to go off
        static long msDisplayTimeout;       // Next backlight time to go off
        static bool BacklightOff = false;
        static bool LCDTurnedOn = false;

        public CapTouchDriver(I2CDevice sharedBus)
        {
            display = new DisplayNhd5(sharedBus);

            //LCDbacklight = new OutputPort(GHI.Pins.G120.P1_19, true);
            //LCDbacklight.Write(true);
            msBacklightTime = 0;            // Default is off
            msDisplayTimeout = 0;

            display.TouchUp += display_TouchUp;
            display.TouchDown += display_TouchDown;
            display.ZoomIn += display_ZoomIn;
            display.ZoomOut += display_ZoomOut;

            touches = new Microsoft.SPOT.Touch.TouchInput[1];
            touches[0] = new TouchInput();
            //
            // Using interrupt (for G120)
            //
            touchPin = new InterruptPort(GHI.Pins.G120.P0_25, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
           
            //touchPin = new InterruptPort(GHI.Pins.G120.P2_21, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            touchPin.OnInterrupt += touchPin_OnInterrupt;
            //
            // Create thread the handle the backlight timeout
            //
            Thread threadBacklight = new Thread(backlightHandler);
            threadBacklight.Start();
        }

        void backlightHandler()
        {
            while (true)
            {
                if (msBacklightTime > 0 && BacklightOff == false)
                {
                    
                    if (UtilsClass.msTime() > msDisplayTimeout)
                    {
                        BacklightOff = true;

                        //LCDbacklight.Write(false);  // Switch it off
                    }
                }
                Thread.Sleep(1000);
            }
        }

        void display_ZoomOut(DisplayNhd5 sender, EventArgs e)
        {
            Debug.Print("Zoom out");
        }

        void display_ZoomIn(DisplayNhd5 sender, EventArgs e)
        {
            Debug.Print("Zoom in");
        }

        static void display_TouchDown(DisplayNhd5 sender, TouchEventArgs e)
        {
//            Debug.Print("Finger " + e.FingerNumber + " down!");
//            Debug.Print("Where " + e.X + "," + e.Y);
            //
            // Check if backlight is off and if so, we will switch it back
            // on and consume the touch
            //
            if (BacklightOff)
            {
                BacklightOff = false;
                //LCDbacklight.Write(true);

                ResetBacklightInternal();   // Reset the timer

                LCDTurnedOn = true;         // We should ignore the touch up
                return;
            }
            else
            {
                ResetBacklightInternal();   // Each touch will reset the timer
            }
            touches[0].X = e.X;
            touches[0].Y = e.Y;
            GlideTouch.RaiseTouchDownEvent(null, new GHI.Glide.TouchEventArgs(touches));
        }

        static void display_TouchUp(DisplayNhd5 sender, TouchEventArgs e)
        {
//            Debug.Print("Finger " + e.FingerNumber + " up!");
            if (LCDTurnedOn)
            {
                LCDTurnedOn = false;
                return;
            }
            touches[0].X = e.X;
            touches[0].Y = e.Y;
            GlideTouch.RaiseTouchUpEvent(null, new GHI.Glide.TouchEventArgs(touches));
        }

        static void touchPin_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            display.ReadAndProcessTouchData();
        }

        public void Calibrate()
        {
            touchPin.OnInterrupt -= touchPin_OnInterrupt;   // Remove the IRQ until we calibrate it

            display.ReadInformation();

            display.Calibrtate();

            touchPin.OnInterrupt += touchPin_OnInterrupt;
        }

        static void ResetBacklightInternal()
        {
            msDisplayTimeout = UtilsClass.msTime() + msBacklightTime;
        }

        public void ResetBacklight()
        {
            msDisplayTimeout = UtilsClass.msTime() + msBacklightTime;
        }

        public void SetBacklightTime(long msTime)
        {
            msBacklightTime = msTime;
        }

        public void SwitchBacklightOff()
        {
            //
            // This will cause the backlight to go off
            //
            msBacklightTime = UtilsClass.msTime();
        }

        public void SwithcBacklightOn()
        {
            //
            // Override the timer and bring it on. 
            //
            BacklightOff = false;
            //LCDbacklight.Write(true);

            ResetBacklightInternal();   // Reset the timer
        }
    }

    public class UtilsClass
    {
        public static long msTime()
        {
            long msTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            return msTime;
        }
    }
}
