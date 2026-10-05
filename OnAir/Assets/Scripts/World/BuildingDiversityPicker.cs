using System;
using System.Collections.Generic;

namespace OnAir
{
    // One history per parcel, shared by frontage and rear infill. Fits and district
    // eligibility are decided by the caller; never invent an incompatible alternative.
    public sealed class BuildingDiversityPicker
    {
        string last;
        int run;
        readonly Queue<string> recent=new Queue<string>();
        public int Choices{get;private set;}
        public int SingleOptionChoices{get;private set;}
        public int Repeats{get;private set;}
        public int ForcedRepeats{get;private set;}
        public SpawnRule Choose(SpawnRule[] candidates,Func<SpawnRule,double> baseWeight,double unitTicket)
        {
            if(candidates.Length==0)throw new ArgumentException("No fitting candidates");
            bool alternative=false;string first=candidates[0].PrefabId;bool multiple=false;
            foreach(var r in candidates){alternative|=r.PrefabId!=last;multiple|=r.PrefabId!=first;}
            double Weight(SpawnRule r)
            {
                if(run>=2&&alternative&&r.PrefabId==last)return 0;
                int seen=0;foreach(var id in recent)if(id==r.PrefabId)seen++;
                return baseWeight(r)*(r.PrefabId==last?.2:1)/(1+seen*.5);
            }
            double total=0;foreach(var r in candidates)total+=Weight(r);
            if(total<=0)throw new ArgumentException("No positive candidate weight");
            double ticket=Math.Max(0,Math.Min(.999999999,unitTicket))*total;
            SpawnRule chosen=null;
            foreach(var r in candidates){double w=Weight(r);if(w<=0)continue;chosen=r;ticket-=w;if(ticket<0)break;}
            Choices++;if(!multiple)SingleOptionChoices++;
            if(chosen.PrefabId==last){Repeats++;if(!alternative)ForcedRepeats++;run++;}else run=1;
            last=chosen.PrefabId;recent.Enqueue(last);if(recent.Count>4)recent.Dequeue();
            return chosen;
        }
    }
}
