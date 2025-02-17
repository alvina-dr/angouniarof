using System;
using System.Text;
using UnityEngine;
using WiimoteApi;
using XCharts.Runtime;

public class WiimoteDemo : MonoBehaviour
{
    public LineChart lineChart;
    
    public WiimoteModel model;
    public RectTransform[] ir_dots;
    public RectTransform[] ir_bb;
    public RectTransform ir_pointer;

    private Quaternion initial_rotation;

    private Wiimote wiimote;

    private Vector2 scrollPosition;

    private Vector3 wmpOffset = Vector3.zero;

    private float _maxMagnitudeOfCurrentShot;

    private void Start()
    {
        initial_rotation = model.rot.localRotation;
        lineChart.ClearData();
    }

    private void Update()
    {
        if (!WiimoteManager.HasWiimote()) return;

        wiimote = WiimoteManager.Wiimotes[0];
        if (wiimote != null) wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);

        int ret;
        do
        {
            ret = wiimote.ReadWiimoteData();

            if (ret > 0 && wiimote.current_ext == ExtensionController.MOTIONPLUS)
            {
                var offset = new Vector3(-wiimote.MotionPlus.PitchSpeed,
                    wiimote.MotionPlus.YawSpeed,
                    wiimote.MotionPlus.RollSpeed) / 95f; // Divide by 95Hz (average updates per second from wiimote)
                wmpOffset += offset;

                model.rot.Rotate(offset, Space.Self);
            }
        } while (ret > 0);
        
        // Put the data inside a Vector3
        Vector3 accel = new Vector3(wiimote.Accel.accel[0], wiimote.Accel.accel[1], wiimote.Accel.accel[2]);
        
        // Get the magnitude of the acceleration
        float magnitude = accel.magnitude;
        
        // Add the magnitude to the line chart
        lineChart.AddData(0, DateTime.Now, magnitude);

        // Length is always 3 (X, Y, Z)
        //Debug.Log($"Accel Data X: {accel.x}, Y: {accel.y}, Z: {accel.z}, Magnitude: {magnitude}");
        if (magnitude > GameManager.Instance.MinimumMagnitude) //Currently shooting
        {
            if (magnitude > _maxMagnitudeOfCurrentShot)
            {
                _maxMagnitudeOfCurrentShot = magnitude;
            }
        }
        else //Currently not shooting
        {
            if (_maxMagnitudeOfCurrentShot != 0)
            {
                GameManager.Instance.ScoreText.SetScore(_maxMagnitudeOfCurrentShot);
                GameManager.Instance.CurrentScore = Mathf.RoundToInt(_maxMagnitudeOfCurrentShot);
            }
            _maxMagnitudeOfCurrentShot = 0;
        }

        if (model.a) model.a.enabled = wiimote.Button.a;
        if (model.b) model.b.enabled = wiimote.Button.b;
        if (model.one) model.one.enabled = wiimote.Button.one;
        if (model.two) model.two.enabled = wiimote.Button.two;
        if (model.d_up) model.d_up.enabled = wiimote.Button.d_up;
        if (model.d_down) model.d_down.enabled = wiimote.Button.d_down;
        if (model.d_left) model.d_left.enabled = wiimote.Button.d_left;
        if (model.d_right) model.d_right.enabled = wiimote.Button.d_right;
        if (model.plus) model.plus.enabled = wiimote.Button.plus;
        if (model.minus) model.minus.enabled = wiimote.Button.minus;
        if (model.home) model.home.enabled = wiimote.Button.home;

        if (wiimote.current_ext != ExtensionController.MOTIONPLUS)
        {
            model.rot.localRotation = initial_rotation;
        }

        if (ir_dots.Length < 4) return;

        var ir = wiimote.Ir.GetProbableSensorBarIR();
        for (var i = 0; i < 2; i++)
        {
            var x = ir[i, 0] / 1023f;
            var y = ir[i, 1] / 767f;
            if (x == -1 || y == -1)
            {
                ir_dots[i].anchorMin = new Vector2(0, 0);
                ir_dots[i].anchorMax = new Vector2(0, 0);
            }

            ir_dots[i].anchorMin = new Vector2(x, y);
            ir_dots[i].anchorMax = new Vector2(x, y);

            if (ir[i, 2] != -1)
            {
                var index = (int)ir[i, 2];
                var xmin = wiimote.Ir.ir[index, 3] / 127f;
                var ymin = wiimote.Ir.ir[index, 4] / 127f;
                var xmax = wiimote.Ir.ir[index, 5] / 127f;
                var ymax = wiimote.Ir.ir[index, 6] / 127f;
                ir_bb[i].anchorMin = new Vector2(xmin, ymin);
                ir_bb[i].anchorMax = new Vector2(xmax, ymax);
            }
        }

        var pointer = wiimote.Ir.GetPointingPosition();
        ir_pointer.anchorMin = new Vector2(pointer[0], pointer[1]);
        ir_pointer.anchorMax = new Vector2(pointer[0], pointer[1]);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(0, 0, 320, Screen.height), "");

        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("Wiimote Found: " + WiimoteManager.HasWiimote());
        if (GUILayout.Button("Find Wiimote"))
        {
            WiimoteManager.FindWiimotes();
            if (wiimote != null)
            {
                wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);
            }

        }

        if (GUILayout.Button("Cleanup"))
        {
            WiimoteManager.Cleanup(wiimote);
            wiimote = null;
        }

        if (wiimote != null)
        {
            GUILayout.Label("Extension: " + wiimote.current_ext);

            GUILayout.Label("LED Test:");
            GUILayout.BeginHorizontal();
            for (var x = 0; x < 4; x++)
                if (GUILayout.Button("" + x, GUILayout.Width(300 / 4)))
                {
                    wiimote.SendPlayerLED(x == 0, x == 1, x == 2, x == 3);
                }
            GUILayout.EndHorizontal();

            GUILayout.Label("Set Report:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("But/Acc", GUILayout.Width(300 / 4)))
            {
                wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL);
            }
            if (GUILayout.Button("But/Ext8", GUILayout.Width(300 / 4)))
            {
                wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_EXT8);
            }
            if (GUILayout.Button("B/A/Ext16", GUILayout.Width(300 / 4)))
            {
                wiimote.SendDataReportMode(InputDataType.REPORT_BUTTONS_ACCEL_EXT16);
            }
            if (GUILayout.Button("Ext21", GUILayout.Width(300 / 4)))
            {
                wiimote.SendDataReportMode(InputDataType.REPORT_EXT21);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Request Status Report"))
            {
                wiimote.SendStatusInfoRequest();
            }

            GUILayout.Label("IR Setup Sequence:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Basic", GUILayout.Width(100)))
            {
                wiimote.SetupIRCamera(IRDataType.BASIC);
            }
            if (GUILayout.Button("Extended", GUILayout.Width(100)))
            {
                wiimote.SetupIRCamera();
            }
            if (GUILayout.Button("Full", GUILayout.Width(100)))
            {
                wiimote.SetupIRCamera(IRDataType.FULL);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("WMP Attached: " + wiimote.wmp_attached);
            if (GUILayout.Button("Request Identify WMP"))
            {
                wiimote.RequestIdentifyWiiMotionPlus();
            }
            if ((wiimote.wmp_attached || wiimote.Type == WiimoteType.PROCONTROLLER) &&
                GUILayout.Button("Activate WMP"))
            {
                wiimote.ActivateWiiMotionPlus();
            }
            if ((wiimote.current_ext == ExtensionController.MOTIONPLUS ||
                 wiimote.current_ext == ExtensionController.MOTIONPLUS_CLASSIC ||
                 wiimote.current_ext == ExtensionController.MOTIONPLUS_NUNCHUCK) &&
                GUILayout.Button("Deactivate WMP"))
            {
                wiimote.DeactivateWiiMotionPlus();
            }

            GUILayout.Label("Calibrate Accelerometer");
            GUILayout.BeginHorizontal();
            for (var x = 0; x < 3; x++)
            {
                var step = (AccelCalibrationStep)x;
                if (GUILayout.Button(step.ToString(), GUILayout.Width(100)))
                {
                    wiimote.Accel.CalibrateAccel(step);
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Print Calibration Data"))
            {
                var str = new StringBuilder();
                for (var x = 0; x < 3; x++)
                {
                    for (var y = 0; y < 3; y++) str.Append(wiimote.Accel.accel_calib[y, x]).Append(" ");
                    str.Append("\n");
                }
                Debug.Log(str.ToString());
            }

            if (wiimote.current_ext != ExtensionController.NONE)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                var bold = new GUIStyle(GUI.skin.button);
                bold.fontStyle = FontStyle.Bold;
                if (wiimote.current_ext == ExtensionController.NUNCHUCK)
                {
                    GUILayout.Label("Nunchuck:", bold);
                    var data = wiimote.Nunchuck;
                    GUILayout.Label("Stick: " + data.stick[0] + ", " + data.stick[1]);
                    GUILayout.Label("C: " + data.c);
                    GUILayout.Label("Z: " + data.z);
                }
                else if (wiimote.current_ext == ExtensionController.CLASSIC)
                {
                    GUILayout.Label("Classic Controller:", bold);
                    var data = wiimote.ClassicController;
                    GUILayout.Label("Stick Left: " + data.lstick[0] + ", " + data.lstick[1]);
                    GUILayout.Label("Stick Right: " + data.rstick[0] + ", " + data.rstick[1]);
                    GUILayout.Label("Trigger Left: " + data.ltrigger_range);
                    GUILayout.Label("Trigger Right: " + data.rtrigger_range);
                    GUILayout.Label("Trigger Left Button: " + data.ltrigger_switch);
                    GUILayout.Label("Trigger Right Button: " + data.rtrigger_switch);
                    GUILayout.Label("A: " + data.a);
                    GUILayout.Label("B: " + data.b);
                    GUILayout.Label("X: " + data.x);
                    GUILayout.Label("Y: " + data.y);
                    GUILayout.Label("Plus: " + data.plus);
                    GUILayout.Label("Minus: " + data.minus);
                    GUILayout.Label("Home: " + data.home);
                    GUILayout.Label("ZL: " + data.zl);
                    GUILayout.Label("ZR: " + data.zr);
                    GUILayout.Label("D-Up: " + data.dpad_up);
                    GUILayout.Label("D-Down: " + data.dpad_down);
                    GUILayout.Label("D-Left: " + data.dpad_left);
                    GUILayout.Label("D-Right: " + data.dpad_right);
                }
                else if (wiimote.current_ext == ExtensionController.MOTIONPLUS)
                {
                    GUILayout.Label("Wii Motion Plus:", bold);
                    var data = wiimote.MotionPlus;
                    GUILayout.Label("Pitch Speed: " + data.PitchSpeed);
                    GUILayout.Label("Yaw Speed: " + data.YawSpeed);
                    GUILayout.Label("Roll Speed: " + data.RollSpeed);
                    GUILayout.Label("Pitch Slow: " + data.PitchSlow);
                    GUILayout.Label("Yaw Slow: " + data.YawSlow);
                    GUILayout.Label("Roll Slow: " + data.RollSlow);
                    if (GUILayout.Button("Zero Out WMP"))
                    {
                        data.SetZeroValues();
                        model.rot.rotation =
                            Quaternion.FromToRotation(model.rot.rotation * GetAccelVector(), Vector3.up) *
                            model.rot.rotation;
                        model.rot.rotation = Quaternion.FromToRotation(model.rot.forward, Vector3.forward) *
                                             model.rot.rotation;
                    }
                    if (GUILayout.Button("Reset Offset"))
                    {
                        wmpOffset = Vector3.zero;
                    }
                    GUILayout.Label("Offset: " + wmpOffset);
                }
                else if (wiimote.current_ext == ExtensionController.WIIU_PRO)
                {
                    GUILayout.Label("Wii U Pro Controller:", bold);
                    var data = wiimote.WiiUPro;
                    GUILayout.Label("Stick Left: " + data.lstick[0] + ", " + data.lstick[1]);
                    GUILayout.Label("Stick Right: " + data.rstick[0] + ", " + data.rstick[1]);
                    GUILayout.Label("A: " + data.a);
                    GUILayout.Label("B: " + data.b);
                    GUILayout.Label("X: " + data.x);
                    GUILayout.Label("Y: " + data.y);
                    GUILayout.Label("D-Up: " + data.dpad_up);
                    GUILayout.Label("D-Down: " + data.dpad_down);
                    GUILayout.Label("D-Left: " + data.dpad_left);
                    GUILayout.Label("D-Right: " + data.dpad_right);
                    GUILayout.Label("Plus: " + data.plus);
                    GUILayout.Label("Minus: " + data.minus);
                    GUILayout.Label("Home: " + data.home);
                    GUILayout.Label("L: " + data.l);
                    GUILayout.Label("R: " + data.r);
                    GUILayout.Label("ZL: " + data.zl);
                    GUILayout.Label("ZR: " + data.zr);
                }
                GUILayout.EndScrollView();
            }
            else
            {
                scrollPosition = Vector2.zero;
            }
        }
        else
        {
            GUILayout.Label("No Wiimote connected.");
        }
        GUILayout.EndVertical();
    }


    private void OnDrawGizmos()
    {
        if (wiimote == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(model.rot.position, model.rot.position + model.rot.rotation * GetAccelVector() * 2);
    }

    private Vector3 GetAccelVector()
    {
        float accel_x;
        float accel_y;
        float accel_z;

        var accel = wiimote.Accel.GetCalibratedAccelData();
        accel_x = accel[0];
        accel_y = -accel[2];
        accel_z = -accel[1];

        return new Vector3(accel_x, accel_y, accel_z).normalized;
    }

    [Serializable]
    public class WiimoteModel
    {
        public Transform rot;
        public Renderer a;
        public Renderer b;
        public Renderer one;
        public Renderer two;
        public Renderer d_up;
        public Renderer d_down;
        public Renderer d_left;
        public Renderer d_right;
        public Renderer plus;
        public Renderer minus;
        public Renderer home;
    }

    private void OnApplicationQuit()
    {
        if (wiimote != null)
        {
            Debug.Log("Cleaning up Wiimote.");
            WiimoteManager.Cleanup(wiimote);
            wiimote = null;
            WiimoteManager.Shutdown();
        }
    }
}