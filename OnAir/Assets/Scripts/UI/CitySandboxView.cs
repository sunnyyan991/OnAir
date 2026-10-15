using System;
using System.Linq;
using UnityEngine;

namespace OnAir
{
    // Menu-only IMGUI: no asset discovery, object previews or generation work per frame.
    public sealed class CitySandboxView
    {
        CityDistrict district=CityDistrict.CBD;
        int biome,dragControl;
        string dragging,hovered;
        bool dragFromEquipped;
        Vector2 equippedScroll,availableScroll;
        CityAssetLoadout shownDraft;
        static readonly string[] Biomes={"CITY","TOWN","COAST","FOREST","HILLS","OCEAN"};
        static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        public void ReleaseDrag(){if(dragControl!=0&&GUIUtility.hotControl==dragControl)GUIUtility.hotControl=0;dragControl=0;dragging=null;}
        public void Draw(GameEntry game,float width,float height)
        {
            var sandbox=game.citySandbox;var art=game.panel;var ev=Event.current;var pointer=ev.mousePosition;
            int control=GUIUtility.GetControlID(47159,FocusType.Passive);
            if(shownDraft!=sandbox.Draft){ReleaseDrag();shownDraft=sandbox.Draft;equippedScroll=availableScroll=Vector2.zero;}
            float w=Mathf.Min(1120,width-24),h=Mathf.Min(690,height-24);
            var box=new Rect((width-w)/2,(height-h)/2,w,h);art.MenuPanel(box);
            Text(art,new Rect(box.x+22,box.y+12,w-44,36),"WORLD ASSET SANDBOX",3);
            Text(art,new Rect(box.x+22,box.y+47,w-44,22),"DEMO - CITY DISTRICT POOLS / NEW FLIGHTS ONLY",1);
            float tabW=(w-44)/Biomes.Length;
            for(int i=0;i<Biomes.Length;i++)
                if(art.MenuButton(new Rect(box.x+22+i*tabW,box.y+79,tabW-5,32),Biomes[i],true,biome==i)){biome=i;ReleaseDrag();}
            var left=new Rect(box.x+22,box.y+127,(w-56)*.55f,h-233);
            var right=new Rect(left.xMax+12,left.y,(w-56)*.45f,left.height);
            art.MenuPanel(left);art.MenuPanel(right);
            if(biome!=0)
            {
                Text(art,new Rect(left.x+12,left.y+22,left.width-24,28),Biomes[biome],3);
                Text(art,new Rect(left.x+12,left.y+65,left.width-24,45),"THIS DEMO EDITS CITY FIRST",2);
                Text(art,new Rect(left.x+12,left.y+110,left.width-24,45),"OTHER BIOMES KEEP THEIR EXISTING RULES",1);
                Text(art,new Rect(right.x+12,right.y+25,right.width-24,28),"CHOOSE CITY TO EQUIP BUILDINGS",1);
            }
            else
            {
                float dw=(left.width-20)/3;
                for(int i=0;i<3;i++)
                {
                    var option=(CityDistrict)(i+1);
                    if(art.MenuButton(new Rect(left.x+10+i*dw,left.y+10,dw-4,34),option.ToString().ToUpperInvariant(),true,district==option))
                    {district=option;equippedScroll=availableScroll=Vector2.zero;ReleaseDrag();}
                }
                if(district==CityDistrict.Airport)DrawAirport(art,sandbox,left,right);
                else if(sandbox.Catalog==null)
                    Text(art,new Rect(left.x+12,left.y+60,left.width-24,70),"ASSET CATALOG NOT AVAILABLE",1);
                else
                {
                    var settings=sandbox.Draft.For(district);
                    var all=sandbox.Catalog.entries;
                    var equipped=all.Where(a=>settings.Contains(a.id)).OrderByDescending(a=>CityAssetLoadout.IsReserve(a.id)).ThenBy(a=>a.code).ToArray();
                    var available=all.Where(a=>!settings.Contains(a.id)).OrderByDescending(a=>CityAssetLoadout.IsReserve(a.id)).ThenBy(a=>a.code).ToArray();
                    Text(art,new Rect(left.x+12,left.y+49,left.width-24,21),"EQUIPPED "+equipped.Length+" / DRAG OUT TO REMOVE",1);
                    Text(art,new Rect(left.x+12,left.y+70,left.width-24,20),district==CityDistrict.CBD?"FOCUS: OFFICES / MALLS / TOWERS":"FOCUS: HOMES / APARTMENTS / LOCAL SHOPS",1);
                    Text(art,new Rect(right.x+12,right.y+13,right.width-24,28),"AVAILABLE FOR "+district.ToString().ToUpperInvariant(),2);
                    Text(art,new Rect(right.x+12,right.y+48,right.width-24,25),"DRAG A BUILDING TO AN EMPTY SLOT",1);
                    hovered=null;
                    var drop=new Rect(left.x+8,left.y+94,left.width-16,left.height-102);
                    var inventory=new Rect(right.x+8,right.y+80,right.width-16,right.height-88);
                    Grid(art,equipped,drop,ref equippedScroll,true,control,pointer,6);
                    Grid(art,available,inventory,ref availableScroll,false,control,pointer,0);
                    if(dragging!=null&&ev.rawType==EventType.MouseUp)
                    {
                        if(drop.Contains(pointer))sandbox.SetEquipped(district,dragging,true);
                        else if(inventory.Contains(pointer)&&dragFromEquipped)sandbox.SetEquipped(district,dragging,false);
                        ReleaseDrag();if(ev.type!=EventType.Used)ev.Use();
                    }
                    if(dragging!=null)
                    {
                        var entry=all.First(a=>a.id==dragging);
                        var old=GUI.color;GUI.color=new Color(1,1,1,.9f);
                        if(entry.thumbnail)GUI.DrawTexture(new Rect(pointer.x-35,pointer.y-48,70,96),entry.thumbnail,ScaleMode.ScaleToFit,true);
                        GUI.color=old;Text(art,new Rect(pointer.x-60,pointer.y+45,120,22),entry.code,1);
                        if(ev.type==EventType.MouseDrag)ev.Use();
                    }
                }
            }
            Text(art,new Rect(box.x+22,box.yMax-92,w-44,24),sandbox.Notice,1);
            if(hovered!=null)Text(art,new Rect(box.x+22,box.yMax-115,w-44,21),hovered,1);
            float bw=(w-64)/3;
            if(art.MenuButton(new Rect(box.x+22,box.yMax-55,bw,34),"RESET DEMO")){sandbox.ResetDraft();ReleaseDrag();}
            if(art.MenuButton(new Rect(box.x+32+bw,box.yMax-55,bw,34),"APPLY TO NEW FLIGHTS",true,true))sandbox.Apply();
            if(art.MenuButton(new Rect(box.x+42+bw*2,box.yMax-55,bw,34),"BACK")){ReleaseDrag();game.menu.Back();}
        }
        void Grid(FlightPanel art,CitySandboxCatalog.Entry[] entries,Rect area,ref Vector2 scroll,bool equipped,int control,Vector2 pointer,int blanks)
        {
            int columns=Mathf.Max(1,Mathf.FloorToInt((area.width-16)/106));float cell=(area.width-16)/columns,row=142;
            int count=entries.Length+blanks,rows=Mathf.CeilToInt(count/(float)columns);
            scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,area.width-16,Mathf.Max(area.height,rows*row)));
            for(int i=0;i<count;i++)
            {
                var r=new Rect((i%columns)*cell+3,(i/columns)*row+3,cell-6,row-6);
                art.MenuFill(r,C("273E45"));art.MenuFill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),C("40585D"));
                // Keep spare slots visible at the top; a large collection must not hide the drop target.
                if(i<blanks){Text(art,new Rect(r.x+8,r.y+38,r.width-16,36),"+",3);Text(art,new Rect(r.x+5,r.yMax-28,r.width-10,20),"DROP HERE",1);continue;}
                var a=entries[i-blanks];
                if(a.thumbnail&&Event.current.type==EventType.Repaint)GUI.DrawTexture(new Rect(r.x+6,r.y+6,r.width-12,95),a.thumbnail,ScaleMode.ScaleToFit,true);
                Text(art,new Rect(r.x+4,r.yMax-32,r.width-8,24),a.code,1);
                if(!equipped&&CityAssetLoadout.IsReserve(a.id))Text(art,new Rect(r.x+4,r.y+4,r.width-8,16),"NEW",1,C("E6CC83"));
                if(r.Contains(Event.current.mousePosition))
                {
                    hovered=a.code+" / "+(equipped?"EQUIPPED":"AVAILABLE");
                    if(GUI.enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
                    {dragging=a.id;dragFromEquipped=equipped;dragControl=control;GUIUtility.hotControl=control;Event.current.Use();}
                }
            }
            if(count==0)Text(art,new Rect(8,24,area.width-32,40),"ALL ASSETS ARE EQUIPPED",1);
            GUI.EndScrollView();
        }
        static void DrawAirport(FlightPanel art,CitySandboxController sandbox,Rect left,Rect right)
        {
            Text(art,new Rect(left.x+12,left.y+54,left.width-24,26),"REGIONAL AIRPORT / FIXED COMPOSITE",1);
            if(sandbox.Catalog&&sandbox.Catalog.airportThumbnail)GUI.DrawTexture(new Rect(left.x+20,left.y+94,left.width-40,left.height-151),sandbox.Catalog.airportThumbnail,ScaleMode.ScaleToFit,true);
            Text(art,new Rect(left.x+12,left.yMax-41,left.width-24,26),"ENV-AIR-002",2);
            Text(art,new Rect(right.x+14,right.y+24,right.width-28,30),"AIRPORT IS A REGIONAL ASSET",2);
            Text(art,new Rect(right.x+14,right.y+78,right.width-28,40),"RARE. CONNECTED TO TOWN / CITY.",1);
            Text(art,new Rect(right.x+14,right.y+135,right.width-28,40),"CHOOSING CITY WILL NOT JUMP TO AN AIRPORT.",1);
            Text(art,new Rect(right.x+14,right.y+198,right.width-28,40),"TERMINAL / TOWER / HANGARS / RUNWAY",1);
            Text(art,new Rect(right.x+14,right.y+260,right.width-28,40),"STREET BUILDINGS CANNOT BE EQUIPPED HERE.",1);
        }
        static void Text(FlightPanel art,Rect r,string value,int size,Color? tint=null)=>art.MenuText(r,value,size,false,tint);
    }
}
