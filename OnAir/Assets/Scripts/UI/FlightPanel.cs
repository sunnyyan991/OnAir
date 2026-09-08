using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed class FlightPanel : MonoBehaviour
    {
        public GameSession context;
        public JourneyController journey;
        public TerrainStreamer terrainWorld;
        public FlightController flight;
        GUIStyle text, small, hit;
        Font font;
        readonly Dictionary<char, Texture2D> glyphs = new Dictionary<char, Texture2D>();
        bool collapsed, details;
        Vector2 scroll;
        public WeatherParticles weatherFx;
        public GameEntry game;
        static readonly Color Ink = C("C2CEC8"), Muted = C("83999B"), Cyan = C("6CAAB4");
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        void Init()
        {
            if (text != null && glyphs.Count > 0) return;
            if (font) Destroy(font);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, 18);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0,0,0,0) };
            text.normal.textColor = Ink;
            small = new GUIStyle(text) { fontSize = 12, fontStyle = FontStyle.Normal, wordWrap = true };
            small.normal.textColor = Muted;
            hit = new GUIStyle();
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:-+>/. ";
            string[] patterns = {
                "0E11111F111111","1E11111E11111E","0F10101010100F","1E11111111111E","1F10101E10101F","1F10101E101010",
                "0F10101711110F","1111111F111111","0E04040404040E","0702020212120C","11121418141211","1010101010101F",
                "111B1515111111","11191513111111","0E11111111110E","1E11111E101010","0E11111115120D","1E11111E141211",
                "0F10100E01011E","1F040404040404","1111111111110E","11111111110A04","11111115151B11","11110A040A1111",
                "11110A04040404","1F01020408101F","0E11131519110E","040C040404040E","0E11010204081F","1E01010601011E",
                "02060A121F0202","1F10101E01011E","0E10101E11110E","1F010204080808","0E11110E11110E","0E11110F01010E",
                "00040400040400","0000001F000000","0004041F040400","10080402040810","01010204081010","00000000000C0C","00000000000000"
            };
            for (int i=0;i<alphabet.Length;i++)
            {
                var tex=new Texture2D(5,7,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point, hideFlags=HideFlags.HideAndDontSave };
                var pixels=new Color[35];
                for (int y=0;y<7;y++)
                {
                    int bits=Convert.ToInt32(patterns[i].Substring(y*2,2),16);
                    for (int x=0;x<5;x++) pixels[(6-y)*5+x]=(bits & (1<<(4-x)))!=0 ? Color.white : Color.clear;
                }
                tex.SetPixels(pixels); tex.Apply(false,true); glyphs.Add(alphabet[i],tex);
            }
        }
        static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
        static void Panel(Rect r)
        {
            Fill(new Rect(r.x+4,r.y+4,r.width,r.height), new Color(0,0,0,.25f));
            Fill(r,C("17272E"));
            Fill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),C("495D61"));
            Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),C("2E3E44"));
        }
        void Label(Rect r, string value, bool dim = false)
        {
            // Keep free-form diagnostics in a wrapping system font; instrument labels use original bitmap glyphs.
            if (value.Contains("\n") || value.Length>40) { GUI.Label(r,value,small); return; }
            if (Event.current.type!=EventType.Repaint) return;
            int size=!dim && text.fontSize>=28 ? 3 : 2;
            float x=text.alignment==TextAnchor.MiddleCenter && !dim ? r.center.x-(value.Length*6-1)*size/2f : r.x;
            float y=Mathf.Round(r.center.y-7*size/2f);
            var old=GUI.color; GUI.color=dim ? Muted : text.normal.textColor;
            foreach (char c in value.ToUpperInvariant())
            {
                if (glyphs.TryGetValue(c,out var tex)) GUI.DrawTexture(new Rect(Mathf.Round(x),y,5*size,7*size),tex);
                x+=6*size;
            }
            GUI.color=old;
        }
        bool Button(Rect r, string value)
        {
            Fill(r,C(r.Contains(Event.current.mousePosition) ? "48626B" : "354B53"));
            Fill(new Rect(r.x,r.y,r.width,1),C("50656A"));
            Fill(new Rect(r.x,r.yMax-2,r.width,2),C("17272E"));
            var alignment = text.alignment; text.alignment = TextAnchor.MiddleCenter;
            Label(r,value); text.alignment = alignment;
            return GUI.Button(r,new GUIContent("",value),hit);
        }
        bool Choice(float x, float y, string name, string value)
        {
            var r = new Rect(x+8,y,270,48);
            Fill(r,C(r.Contains(Event.current.mousePosition) ? "354C54" : "22333B"));
            Fill(new Rect(r.x,r.yMax-1,r.width,1),C("44585E"));
            Label(new Rect(x+17,y+3,230,17),name,true);
            Label(new Rect(x+17,y+20,230,25),value.ToUpperInvariant());
            var old = text.normal.textColor; text.normal.textColor = Cyan;
            Label(new Rect(x+252,y+12,20,26),">"); text.normal.textColor = old;
            return GUI.Button(r,new GUIContent("","Cycle " + name),hit);
        }
        void OnGUI()
        {
            if (!context || !journey || !terrainWorld) return;
            Init();
            var matrix = GUI.matrix; var color = GUI.color;
            float scale = Mathf.Min(Screen.width/960f,Screen.height/800f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1)); GUI.color = Color.white;
            try
            {
                Panel(new Rect(20,20,208,70));
                Label(new Rect(34,27,180,18),"LOCAL TIME",true);
                text.fontSize = 28; text.normal.textColor = C("E6CC83");
                Label(new Rect(34,47,180,32),DateTime.Now.ToString("HH:mm:ss"));
                text.fontSize = 18; text.normal.textColor = Ink;
                float x = Screen.width/scale-306;
                Panel(new Rect(x,20,286,collapsed ? 42 : 376));
                Fill(new Rect(x+3,23,280,36),C("213139"));
                Label(new Rect(x+15,26,225,30),"FLIGHT CONTROL");
                if (Button(new Rect(x+249,28,27,24),collapsed ? "+" : "-")) collapsed = !collapsed;
                if (collapsed) return;
                Label(new Rect(x+15,70,250,20),"ENVIRONMENT",true);
                if (Choice(x,94,"TERRAIN",context.biome.ToString())) journey.NextTerrain();
                if (Choice(x,148,"WEATHER",context.weather.ToString())) journey.CycleWeather();
                if (Choice(x,202,"TIME OF DAY",context.period.ToString())) context.CyclePeriod();
                var toggle = new Rect(x+8,258,270,40);
                Fill(toggle,C("22333B")); Label(new Rect(x+17,264,200,28),"AUTO FLIGHT");
                Fill(new Rect(x+226,268,42,21),context.paused ? C("192A31") : C("326574"));
                Fill(new Rect(x+(context.paused ? 229 : 250),271,15,15),context.paused ? Muted : Ink);
                if (GUI.Button(toggle,new GUIContent("","Pause / resume flight"),hit)) context.TogglePause();
                text.fontSize = 14;
                if (Button(new Rect(x+12,311,126,34),"NEW SEED")) journey.NewSeed();
                if (Button(new Rect(x+146,311,128,34),"RELOAD CSV")) game.ReloadTables();
                text.fontSize = 18;
                Fill(new Rect(x+16,366,5,5),context.paused ? C("E6CC83") : Cyan);
                Label(new Rect(x+29,357,245,23),context.paused ? "FLIGHT PAUSED" : "FLIGHT ACTIVE",true);
                Panel(new Rect(x,410,286,details ? 140 : 106));
                Fill(new Rect(x+3,413,280,32),C("213139"));
                Label(new Rect(x+15,415,225,28),"WORLD STATUS");
                if (Button(new Rect(x+249,417,27,24),details ? "-" : "+")) details=!details;
                Label(new Rect(x+15,454,140,22),"BLDGS " + terrainWorld.ActiveBuildingCount,true);
                Label(new Rect(x+158,454,110,22),"LEG "+(journey.Current.index+1),true);
                Label(new Rect(x+15,480,250,22),"ROUTE "+journey.TotalMeters.ToString("0")+" M",true);
                if(flight)
                {
                    if(Button(new Rect(x,details?564:530,286,36),flight.IsCircling?"CIRCLING":"CIRCLE ONCE"))flight.BeginCircle();
                }
                if (details)
                {
                    string status=("Next terrain in "+(journey.Current.terrain.distanceMeters-journey.SegmentMeters).ToString("0")+" m")+(weatherFx ? "\n"+weatherFx.Status : "");
                    float h=Mathf.Max(37,small.CalcHeight(new GUIContent(status),240));
                    scroll=GUI.BeginScrollView(new Rect(x+12,503,262,37),scroll,new Rect(0,0,240,h));
                    GUI.Label(new Rect(0,0,240,h),status,small); GUI.EndScrollView();
                }
            }
            finally { GUI.matrix=matrix; GUI.color=color; }
        }
        void OnDestroy()
        {
            if (font) Destroy(font);
            foreach (var tex in glyphs.Values) if (tex) Destroy(tex);
            glyphs.Clear();
        }
    }
}
