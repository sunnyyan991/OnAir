using System;
using System.Linq;
using UnityEngine;

namespace OnAir
{
    public sealed class MenuView : MonoBehaviour
    {
        public GameEntry game;
        Vector2 slotScroll;
        Texture2D wordmark, planeMark;
        GUIStyle titleHit;
        static readonly Color TitleInk=C("F3EDD1"), TitleGold=C("E6CC83");
        MenuController menu => game.menu;
        FlightPanel art => game.panel;
        static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;}
        void Text(float x,float y,float w,string value,int size=2,bool centered=false)=>art.MenuText(new Rect(x,y,w,28),value,size,centered);
        bool Button(float x,float y,float w,string value,bool enabled=true,bool focus=false)=>art.MenuButton(new Rect(x,y,w,38),value,enabled,focus);
        Rect Center(float w,float h,float width,float height)=>new Rect((width-w)*.5f,(height-h)*.5f,w,h);
        void OnGUI()
        {
            if(!game||!menu||!game.display)return;
            GUI.depth=-20;var matrix=GUI.matrix;var color=GUI.color;bool oldEnabled=GUI.enabled;
            float scale=Mathf.Max(.01f,game.display.UiScale),width=Screen.width/scale,height=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.color=Color.white;
            try
            {
                var ev=Event.current;
                if(art.ReleaseCapturedInput(ev)&&ev.type!=EventType.Used)ev.Use();
                if(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape){menu.Escape();ev.Use();}
                bool modal=menu.OverwriteOpen||menu.Leaving!=LeaveTarget.None||menu.TitleQuitOpen||game.display.ModePending;
                if(modal&&ev.type==EventType.KeyDown&&(ev.keyCode==KeyCode.Return||ev.keyCode==KeyCode.KeypadEnter))
                { if(game.display.ModePending)game.display.RevertMode();else menu.CancelLeave();ev.Use(); }
                GUI.enabled=!modal&&!menu.DisplayOpen;
                if(menu.ShowHud)DrawRunning();
                else
                {
                    bool preparing=menu.Page==MenuPage.Preparing||menu.Page==MenuPage.PrepareFailed;
                    art.MenuFill(new Rect(0,0,width,height),preparing?new Color(.08f,.14f,.17f,1):new Color(.02f,.08f,.1f,.26f));
                    switch(menu.Page)
                    {
                        case MenuPage.Title:DrawTitle(width);break;
                        case MenuPage.NewFlight:case MenuPage.LoadGame:DrawSlots(width,height);break;
                        case MenuPage.Credits:DrawCredits(width,height);break;
                        case MenuPage.Preparing:case MenuPage.PrepareFailed:DrawPreparation(width,height);break;
                    }
                }
                if(menu.DisplayOpen){GUI.enabled=!modal;DrawDisplay(width,height);}
                GUI.enabled=true;
                if(modal)DrawConfirmation(width,height);
                if((menu.BlocksHud||modal)&&ev.isMouse)ev.Use();
            }
            finally {GUI.matrix=matrix;GUI.color=color;GUI.enabled=oldEnabled;}
        }
        void DrawTitle(float width)
        {
            float x=60;
            DrawTitleBrand(x-6,102);
            int focus=0;
            if(GUI.enabled)
                for(int i=0;i<5;i++)
                    if(new Rect(x,280+i*82,250,64).Contains(Event.current.mousePosition))focus=i;
            if(TitleButton(new Rect(x,280,250,64),"START",focus==0))menu.OpenSlots(true);
            if(TitleButton(new Rect(x,362,250,64),"LOAD GAME",focus==1))menu.OpenSlots(false);
            if(TitleButton(new Rect(x,444,250,64),"CREDITS",focus==2))menu.ShowCredits();
            if(TitleButton(new Rect(x,526,250,64),"SETTINGS",focus==3))menu.OpenDisplay();
            if(TitleButton(new Rect(x,608,250,64),"QUIT",focus==4))menu.AskLeave(LeaveTarget.Quit);
        }
        // The title has its own brand glyphs; the existing HUD alphabet stays unchanged.
        void DrawTitleBrand(float x,float y)
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(!wordmark)
            {
                string[][] letters={
                    new[]{"..####..",".######.","###..###","##....##","##....##","##....##","##....##","###..###",".######.","..####.."},
                    new[]{"......","......","......","##.##.","######","###.##","##..##","##..##","##..##","##..##"},
                    new[]{"..####..",".######.","###..###","##....##","##....##","########","########","##....##","##....##","##....##"},
                    new[]{"..","..","##","##","..","##","##","##","##","##"},
                    new[]{".....",".....",".....",".....","##.##","#####","###..","##...","##...","##..."}
                };
                var rows=new string[10];
                for(int row=0;row<rows.Length;row++)
                    rows[row]=string.Join(".",letters.Select(letter=>letter[row]));
                wordmark=PixelMask(rows);
                planeMark=PixelMask(new[]{"....#......","....##.....",".#..###....",".#..###....","###########",".#..###....",".#..###....","....##.....","....#......"});
            }
            var old=GUI.color;
            GUI.color=new Color(.04f,.10f,.12f,.35f);
            GUI.DrawTexture(new Rect(x+2,y+3,264,80),wordmark);
            GUI.color=TitleInk;GUI.DrawTexture(new Rect(x,y,264,80),wordmark);
            GUI.color=old;
            for(int i=0;i<27;i++)art.MenuFill(new Rect(x+i*8,y+111,4,3),C("78908B"));
            GUI.color=TitleInk;GUI.DrawTexture(new Rect(x+224,y+99,33,27),planeMark);GUI.color=old;
        }
        static Texture2D PixelMask(string[] rows)
        {
            int width=rows[0].Length,height=rows.Length;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
                {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)
                for(int x=0;x<width;x++)pixels[(height-1-y)*width+x]=rows[y][x]=='#'?Color.white:Color.clear;
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        void TitleCutRect(Rect r,float cut,Color tint)
        {
            art.MenuFill(new Rect(r.x+cut,r.y,r.width-2*cut,r.height),tint);
            art.MenuFill(new Rect(r.x,r.y+cut,cut,r.height-2*cut),tint);
            art.MenuFill(new Rect(r.xMax-cut,r.y+cut,cut,r.height-2*cut),tint);
        }
        static Rect Inset(Rect rect,float amount)=>new Rect(rect.x+amount,rect.y+amount,rect.width-2*amount,rect.height-2*amount);
        bool TitleButton(Rect rect,string value,bool focused)
        {
            bool usable=GUI.enabled;
            focused&=usable;
            bool hover=usable&&rect.Contains(Event.current.mousePosition);
            if(Event.current.type==EventType.Repaint)
            {
                TitleCutRect(new Rect(rect.x+4,rect.y+5,rect.width,rect.height),4,new Color(.02f,.07f,.09f,.55f));
                TitleCutRect(rect,4,C("142A31"));
                TitleCutRect(Inset(rect,1),3,C("344F56"));
                TitleCutRect(Inset(rect,3),2,focused?TitleGold:C("6B8587"));
                TitleCutRect(Inset(rect,5),1,C("142B32"));
                TitleCutRect(Inset(rect,7),0,C(hover?"304B51":"243C43"));
                art.MenuFill(new Rect(rect.x+8,rect.y+7,rect.width-16,1),C("3D565B"));
                art.MenuFill(new Rect(rect.x+8,rect.yMax-8,rect.width-16,1),C("1B3138"));
                art.MenuText(Inset(rect,9),value,3,true,usable?(focused?TitleInk:C("C2D0CD")):C("83999B"));
                if(focused)
                {
                    float arrowX=rect.x-26,arrowY=rect.center.y-14;
                    art.MenuFill(new Rect(arrowX,arrowY,4,28),TitleGold);
                    art.MenuFill(new Rect(arrowX+4,arrowY+4,4,20),TitleGold);
                    art.MenuFill(new Rect(arrowX+8,arrowY+8,4,12),TitleGold);
                    art.MenuFill(new Rect(arrowX+12,arrowY+12,4,4),TitleGold);
                }
            }
            if(titleHit==null)titleHit=new GUIStyle();
            return GUI.Button(rect,new GUIContent("",value),titleHit);
        }
        void OnDestroy()
        {
            if(wordmark)Destroy(wordmark);
            if(planeMark)Destroy(planeMark);
        }
        void DrawRunning()
        {
            if(Button(20,170,208,menu.SettingsOpen?"SETTINGS -":"SETTINGS +"))menu.ToggleSettings();
            if(!menu.SettingsOpen)return;
            art.MenuPanel(new Rect(20,213,270,237));
            if(Button(30,223,250,"SAVE GAME"))menu.SaveGame();
            if(Button(30,267,250,"DISPLAY"))menu.OpenDisplay();
            art.MenuFill(new Rect(35,312,240,1),C("50656A"));
            if(Button(30,322,250,"BACK TO TITLE"))menu.AskLeave(LeaveTarget.Title);
            if(Button(30,366,250,"QUIT"))menu.AskLeave(LeaveTarget.Quit);
            Text(35,409,240,menu.Notice,1);
            if(menu.BlocksHud)return;
            var ev=Event.current;
            if(ev.type==EventType.MouseDown&&!new Rect(20,170,270,280).Contains(ev.mousePosition))
            { menu.ToggleSettings();ev.Use(); }
            else if(ev.isMouse&&new Rect(20,170,270,280).Contains(ev.mousePosition))ev.Use();
        }
        void DrawSlots(float width,float height)
        {
            bool create=menu.Page==MenuPage.NewFlight;
            var box=Center(Mathf.Min(660,width-40),520,width,height);art.MenuPanel(box);
            Text(box.x+24,box.y+22,box.width-48,create?"NEW FLIGHT":"LOAD GAME",4);
            var area=new Rect(box.x+16,box.y+76,box.width-32,310);
            slotScroll=GUI.BeginScrollView(area,slotScroll,new Rect(0,0,area.width-18,Mathf.Max(area.height,menu.Slots.Count*98)));
            for(int i=0;i<menu.Slots.Count;i++)
            {
                var slot=menu.Slots[i];float y=i*98;var row=new Rect(2,y+2,area.width-24,90);
                bool selected=menu.Selected==i;
                if(art.MenuButton(row,"",!create||!slot.IsLegacy,selected))menu.Select(i);
                if(!slot.occupied){Text(18,y+30,row.width-24,"EMPTY SLOT",2);continue;}
                Text(18,y+8,row.width*.4f,slot.name,2);
                Text(18,y+40,row.width-36,slot.compatible?slot.biome:slot.error,2);
                if(slot.compatible)
                {
                    Text(row.width*.45f,y+8,row.width*.5f,"ROUTE "+(slot.meters/1000).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" KM",2);
                    string time=DateTimeOffset.TryParse(slot.savedAt,out var date)?date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"):"UNKNOWN TIME";
                    Text(18,y+62,row.width-36,"SAVED "+time,2);
                }
            }
            GUI.EndScrollView();
            bool occupied=menu.Slots.Any(s=>s.occupied);
            Text(box.x+24,box.y+397,box.width-48,!create&&!occupied?"NO SAVED FLIGHTS":menu.Notice,1);
            bool can=menu.Selected>=0&&menu.Selected<menu.Slots.Count&&(create?!menu.Slots[menu.Selected].IsLegacy:menu.Slots[menu.Selected].occupied);
            if(Button(box.x+28,box.y+451,box.width*.45f,create?"START":occupied?"LOAD":"NEW FLIGHT",can||(!create&&!occupied),true))
            {if(!create&&!occupied)menu.OpenSlots(true);else menu.StartSelected();}
            if(Button(box.center.x+12,box.y+451,box.width*.45f-16,"BACK"))menu.Back();
        }
        void DrawCredits(float width,float height)
        {
            var box=Center(520,350,width,height);art.MenuPanel(box);
            Text(box.x+30,box.y+24,460,"CREDITS",4);
            Text(box.x+30,box.y+90,460,"CREATED BY",2);
            Text(box.x+30,box.y+138,460,"Moby",3);
            Text(box.x+30,box.y+184,460,"Kisa",3);
            if(Button(box.x+30,box.y+278,460,"BACK"))menu.Back();
        }
        void DrawPreparation(float width,float height)
        {
            var box=Center(520,230,width,height);art.MenuPanel(box);
            Text(box.x+24,box.y+35,472,menu.Notice,2,true);
            if(menu.Page==MenuPage.Preparing)
            {
                Text(box.x+24,box.y+76,472,menu.PreparationStatus,2,true);
                if(menu.PreparationStatus=="BUILDING WORLD")
                {
                    Text(box.x+24,box.y+108,472,Mathf.FloorToInt(game.world.PreparationProgress*100)+"%",2,true);
                    art.MenuFill(new Rect(box.x+24,box.y+151,472,12),C("17272E"));
                    art.MenuFill(new Rect(box.x+24,box.y+151,472*game.world.PreparationProgress,12),C("6CAAB4"));
                }
            }
            if(menu.Page==MenuPage.PrepareFailed)
            {
                if(Button(box.x+24,box.y+135,220,"RETRY"))menu.Retry();
                if(Button(box.x+276,box.y+135,220,"BACK"))menu.Back();
            }
        }
        void DrawDisplay(float width,float height)
        {
            art.MenuFill(new Rect(0,0,width,height),new Color(0,0,0,.35f));
            var box=Center(560,410,width,height);art.MenuPanel(box);var s=game.display.Settings;
            Text(box.x+24,box.y+22,512,"DISPLAY",4);
            if(Button(box.x+24,box.y+88,512,"WINDOW MODE: "+(s.windowMode==0?"WINDOWED":"BORDERLESS")))game.display.PreviewMode();
            if(Button(box.x+24,box.y+143,512,"UI SCALE: "+s.uiScale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)))game.display.CycleScale();
            if(Button(box.x+24,box.y+198,512,"FRAME LIMIT: "+(s.frameLimit==0?"DEFAULT":s.frameLimit<0?"UNLIMITED":s.frameLimit.ToString())))game.display.CycleRate();
            Text(box.x+24,box.y+253,512,game.display.Status,1);
            if(Button(box.x+24,box.y+330,242,"RESET DEFAULTS"))game.display.ResetDefaults();
            if(Button(box.x+294,box.y+330,242,"BACK"))menu.Back();
        }
        void DrawConfirmation(float width,float height)
        {
            art.MenuFill(new Rect(0,0,width,height),new Color(0,0,0,.5f));
            var box=Center(580,310,width,height);art.MenuPanel(box);string prompt;
            if(game.display.ModePending)prompt="KEEP THIS DISPLAY MODE? "+Mathf.CeilToInt(game.display.SecondsRemaining);
            else if(menu.OverwriteOpen)prompt="OVERWRITE THIS SAVE?";
            else if(menu.TitleQuitOpen)prompt="QUIT GAME?";
            else prompt=menu.Leaving==LeaveTarget.Title?"SAVE BEFORE RETURNING TO TITLE?":"SAVE BEFORE QUITTING?";
            Text(box.x+24,box.y+30,532,prompt,2,true);
            Text(box.x+24,box.y+78,532,menu.Notice,1,true);
            if(game.display.ModePending)
            {
                if(Button(box.x+24,box.y+150,250,"KEEP"))game.display.ConfirmMode();
                if(Button(box.x+306,box.y+150,250,"REVERT",true,true))game.display.RevertMode();
            }
            else if(menu.OverwriteOpen||menu.TitleQuitOpen)
            {
                if(Button(box.x+24,box.y+150,250,menu.OverwriteOpen?"OVERWRITE":"QUIT"))
                {if(menu.OverwriteOpen)menu.StartSelected(true);else menu.Quit();}
                if(Button(box.x+306,box.y+150,250,"CANCEL",true,true))menu.CancelLeave();
            }
            else
            {
                bool title=menu.Leaving==LeaveTarget.Title;
                if(Button(box.x+24,box.y+128,532,title?"SAVE AND RETURN":"SAVE AND QUIT"))menu.CompleteLeave(true);
                if(Button(box.x+24,box.y+178,532,title?"RETURN WITHOUT SAVING":"QUIT WITHOUT SAVING"))menu.CompleteLeave(false);
                if(Button(box.x+24,box.y+228,532,"CANCEL",true,true))menu.CancelLeave();
            }
        }
    }
}
