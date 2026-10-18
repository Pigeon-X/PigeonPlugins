using Terraria;
using UnifierTSL.Servers;

namespace CommandTool;

internal readonly record struct ToolEntity(int X,int Y,int Width,int Height,int SignIndex=-1,string? Text=null)
{
    internal bool Contains(int x,int y)=>x>=X&&x<X+Width&&y>=Y&&y<Y+Height;
}
internal static class ToolEntities
{
    internal static ToolEntity? Sign(ServerContext server,int x,int y)
    {
        if(!Bounds(server,x,y))return null;
        int index=Terraria.Sign.ReadSign(server,x,y,false);
        if(index<0||index>=server.Main.sign.Length)return null;
        var sign=server.Main.sign[index];if(sign is null)return null;
        return new(sign.x,sign.y,2,2,index,sign.text??"");
    }
    internal static ToolEntity? Switch(ServerContext server,int x,int y,bool forProtection=false)
    {
        if(!Bounds(server,x,y))return null;
        var t=server.Main.tile[x,y];if(!t.IsNotNull||!(forProtection?t.active():t.nactive()))return null;
        // 136小开关、135压力板都是1x1可触发实体；132是2x2拉杆。
        if(t.type==136||t.type==135)return new(x,y,1,1);
        if(t.type!=132)return null;
        int left=x-(t.frameX/18%2),top=y-(t.frameY/18%2);
        if(!Bounds(server,left,top)||!Bounds(server,left+1,top+1))return null;
        return new(left,top,2,2);
    }
    internal static bool Bounds(ServerContext s,int x,int y)=>x>=0&&y>=0&&x<s.Main.maxTilesX&&y<s.Main.maxTilesY;
    internal static bool Near(ToolActor actor,int x,int y)=>MathF.Abs(actor.Player.Center.X/16-x)<=16
        &&MathF.Abs(actor.Player.Center.Y/16-y)<=16;
}
