﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DualSenseBatteryMonitor
{
    public partial class controllerWidget : Page
    {
        //Controller index (1-based)
        private int self_index;

        //Opacity values for connected and disconnected states
        private const float enabled_opacity = 0.7f;
        private const float disabled_opacity = 0.4f; //Kinda deprecated

        //Controller icon cache to reduce memory allocations
        private static readonly BitmapImage ControllerConnectedIcon = new BitmapImage(new Uri(@"/icons/controller/dualsense_connected.png", UriKind.Relative));
        private static readonly BitmapImage ControllerDisconnectedIcon = new BitmapImage(new Uri(@"/icons/controller/dualsense_not_connected.png", UriKind.Relative));

        //HueToGradient cache to avoid creating duplicate gradient brushes for similar hues
        private static readonly Dictionary<int, LinearGradientBrush> gradientCache = new();

        //Player colors
        private LinearGradientBrush color_player_01 = new LinearGradientBrush();
        private LinearGradientBrush color_player_02 = new LinearGradientBrush();
        private LinearGradientBrush color_player_03 = new LinearGradientBrush();
        private LinearGradientBrush color_player_04 = new LinearGradientBrush();
        private List<Brush> playerColors;

        //Controls animation for low battery status
        private bool isPlayingLowBatAnim = false;
        //Low battery anim threshold
        private const int lowBatteryThreshold = 15;
        //cached storyboard reference
        private Storyboard blink_storyboard;

        // Constructor
        public controllerWidget(bool noControllers, int index, int batterylevel)
        {
            self_index = index + 1;

            InitializePlayerColors();
            InitializeComponent();

            //Load animation from XAML resources
            blink_storyboard = (Storyboard)Resources["BlinkStoryboard"];

            //Apply player-specific color to index text
            updatePlayerColor();

            //Set index in text block
            count_index.Text = self_index.ToString();
        }

        //Updates the widget based on controller count, battery level, and charging status
        public void RefreshData(int controllerAmount, int batterylevel, bool isCharging)
        {
            //no Controllers
            if (controllerAmount == 0)
            {
                if (self_index != 0) 
                {
                    //Hide widget for unused player slots
                    Visibility = Visibility.Collapsed;
                    //If widget is hidden, no need to update the icons

                    UpdateBatteryAnim(0, true); //Stop blinking to improve performance
                } else
                {
                    //No controllers, show "disconnected" icon
                    //Will still not be visible as the application is not visible when no dualsense controllers are plugged in, kinda useless code now :\
                    Visibility = Visibility.Visible;

                    //Set controller icon using cached icon
                    if (image_controller_icon.Source != ControllerDisconnectedIcon)
                        image_controller_icon.Source = ControllerDisconnectedIcon;

                    //Make icon darker when no controller is connected
                    image_controller_icon.Opacity = disabled_opacity;

                    progressbar_battery.Value = 0;
                    progressbar_battery.Foreground = HueToGradient(batterylevel);

                    SetChargingIconActive(isCharging);

                    //Use true instead of isCharging, so it won't play the animation when nothing is rendering
                    UpdateBatteryAnim(batterylevel, true);
                }
            }
            else
            {
                //If this controller is connected
                if (controllerAmount >= self_index)
                {
                    //Show controller as connected
                    Visibility = Visibility.Visible;

                    //Set controller icon using cached icon
                    if (image_controller_icon.Source != ControllerConnectedIcon)
                        image_controller_icon.Source = ControllerConnectedIcon;

                    image_controller_icon.Opacity = enabled_opacity;

                    //If the battery level is used to display a error code
                    if (batterylevel > 500)
                    {
                        //Make sure the battery icon is not visible
                        icon_battery.Visibility = Visibility.Hidden;
                        progressbar_battery.Visibility = Visibility.Hidden;

                        //Make the charging icon dissapear
                        SetChargingIconActive(false);

                        //Make sure the battery anim is stopped, to same performance on something that is not being rendered
                        UpdateBatteryAnim(batterylevel, false);

                        //The batterylevel contains the error code
                        SetDebugErrorCode(batterylevel, true);
                    } 
                    else //No error code
                    {
                        //Make sure the battery icon is visible
                        icon_battery.Visibility = Visibility.Visible;
                        progressbar_battery.Visibility = Visibility.Visible;

                        progressbar_battery.Foreground = HueToGradient(batterylevel);
                        progressbar_battery.Value = batterylevel;

                        //Set the charging icon when there is no error code
                        SetChargingIconActive(isCharging);

                        //Update if the battery blinking anim should play
                        UpdateBatteryAnim(batterylevel, isCharging);

                        //Make debug error code stop displaying
                        SetDebugErrorCode(0, false);
                    }
                }
                else
                {
                    //Not connected for this index, hide it
                    Visibility = Visibility.Collapsed;

                    UpdateBatteryAnim(0, true); //Stop blinking to improve performance
                }
            }
        }

        //Initializes the player color gradients
        private void InitializePlayerColors()
        {
            //Player 01
            color_player_01.StartPoint = new Point(0.5, 0);
            color_player_01.EndPoint = new Point(0.5, 1);
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00bfff"), 0.0));
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#007399"), 1.0));

            color_player_01.Freeze();

            //Player 02
            color_player_02.StartPoint = new Point(0.5, 0);
            color_player_02.EndPoint = new Point(0.5, 1);
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff0000"), 0.0));
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990000"), 1.0));

            color_player_02.Freeze();

            //Player 03
            color_player_03.StartPoint = new Point(0.5, 0);
            color_player_03.EndPoint = new Point(0.5, 1);
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00ff40"), 0.0));
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#009926"), 1.0));

            color_player_03.Freeze();

            //Player 04
            color_player_04.StartPoint = new Point(0.5, 0);
            color_player_04.EndPoint = new Point(0.5, 1);
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff00ff"), 0.0));
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990099"), 1.0));

            color_player_04.Freeze();

            playerColors = new List<Brush> {color_player_01, color_player_02, color_player_03, color_player_04};
        }

        //Controls blinking animation for low battery
        private void UpdateBatteryAnim(int batterylevel, bool isCharging)
        {
            //is the battery level lower then the threshold
            if (batterylevel < lowBatteryThreshold)
            {
                if (!isCharging)
                {
                    // only begin if not already running
                    if (!isPlayingLowBatAnim)
                    {
                        // true: isControllable so we can stop it later
                        blink_storyboard.Begin(this, true);
                        isPlayingLowBatAnim = true;

                        //Display battery warning icon is not already
                        if (icon_batterywarning.Visibility == Visibility.Collapsed)
                        {
                            icon_batterywarning.Visibility = Visibility.Visible;
                        }
                    }
                } else
                {
                    blink_storyboard.Stop(this);
                    isPlayingLowBatAnim = false;

                    //Make battery warning invisible when charging
                    if (icon_batterywarning.Visibility == Visibility.Visible)
                    {
                        icon_batterywarning.Visibility = Visibility.Hidden;
                    }
                }
            }
            //Battery level is higher
            else
            {
                blink_storyboard.Stop(this);
                isPlayingLowBatAnim = false;

                //Make battery warning invisible when on higher battery charge level
                if (icon_batterywarning.Visibility == Visibility.Visible)
                {
                    icon_batterywarning.Visibility = Visibility.Hidden;
                }
            }
        }

        //Assigns color to player index label
        private void updatePlayerColor()
        {
            if (self_index >= 1 && self_index <= playerColors.Count)
                count_index.Foreground = playerColors[self_index - 1];
        }

        //Shows or hides the charging icon
        private void SetChargingIconActive(bool charging)
        {
            if (charging)
            {
                icon_charging.Visibility = Visibility.Visible;
            }
            else
            {
                icon_charging.Visibility = Visibility.Collapsed;
            }

        }

        //This function is used to make the error code contained in the battery level variable visible as text
        private void SetDebugErrorCode(int errorCode, bool shouldShow)
        {
            if (shouldShow)
            {
                debug_errorcode_text.Text = errorCode.ToString();

                if (debug_errorcode_text.Visibility == Visibility.Collapsed)
                {
                    debug_errorcode_text.Visibility = Visibility.Visible;
                    debug_errorcode_text_text.Visibility = Visibility.Visible;
                }
            }
            else
            {
                //Make the visibility command only run if the text is visible
                 if (debug_errorcode_text.Visibility == Visibility.Visible)
                {
                    debug_errorcode_text.Visibility = Visibility.Collapsed;
                    debug_errorcode_text_text.Visibility = Visibility.Collapsed;
                }
            }
        }

        //Converts battery level (as hue) to a gradient
        private static LinearGradientBrush HueToGradient(double hue)
        {
            int key = (int)Math.Round(hue); //Reduce cache keys to avoid high memory usage

            //Try getting a gradient brush from the cache
            if (!gradientCache.TryGetValue(key, out var brush))
            {
                //No gradient brush for the hue was found
                //Create new gradient brush
                brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };

                //Add colors to gradient
                brush.GradientStops.Add(new GradientStop(ColorFromHue(hue, 0.7), 0.0));
                brush.GradientStops.Add(new GradientStop(ColorFromHue(hue, 0.9), 1.0));
                
                //Freeze the brush
                brush.Freeze();

                //Add brush to gradientcache
                gradientCache[key] = brush;
            }

            return brush;
        }


        //Converts hue to a color
        private static Color ColorFromHue(double hue, double darkness)
        {
            // Normalize hue to [0, 360)
            hue = hue % 360;
            if (hue < 0) hue += 360;

            //Offset color
            hue = 0 - (40-(hue*2));
            //Clamp to range of 0-100
            hue = Math.Clamp(hue, 0, 100);

            double s = 1.0; // Full saturation
            double v = darkness;

            int hi = (int)(hue / 60) % 6;
            double f = (hue / 60) - Math.Floor(hue / 60);

            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;

            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            return Color.FromRgb((byte)(r * 255),(byte)(g * 255),(byte)(b * 255));
        }
    }
}
