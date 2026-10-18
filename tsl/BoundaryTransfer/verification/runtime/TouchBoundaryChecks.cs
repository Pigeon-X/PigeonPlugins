using BoundaryTransfer;
using Microsoft.Xna.Framework;
using Terraria;
using UnifierTSL.Servers;

static partial class Program
{
    static void TouchBoundaryChecks(ServerContext server, Player player, Action<string,bool> check)
    {
        Vector2 originalPosition=player.position, originalVelocity=player.velocity;
        bool oldLeft=player.controlLeft, oldRight=player.controlRight;
        try
        {
            player.position=new(server.Main.leftWorld+620,1100*16-player.height);
            player.velocity=new(-3,0);
            player.controlLeft=true; player.controlRight=false;
            player.BordersMovement(server);
            check("真实BordersMovement左身体夹到640像素",player.position.X==server.Main.leftWorld+640 && player.velocity.X==0);
            check("左侧真实触边即命中不使用中心点",BoundaryContact.Side(player.position.X,player.width,server.Main.leftWorld,server.Main.rightWorld,2)==BoundarySide.Left);
            check("顶边速度归零仍可由向外按键触发",BoundaryContact.IsOutward(BoundarySide.Left,player.velocity.X,player.controlLeft,player.controlRight));
            player.controlLeft=false;
            check("无速度无朝外输入时不触发切服",!BoundaryContact.IsOutward(BoundarySide.Left,player.velocity.X,false,false));
            player.controlRight=true;
            check("在左边界向内按键不被传走",!BoundaryContact.IsOutward(BoundarySide.Left,-3,false,true));
            player.position=new(server.Main.rightWorld-620,player.position.Y);
            player.velocity=new(3,0);
            player.controlRight=true; player.controlLeft=false;
            player.BordersMovement(server);
            check("真实BordersMovement右身体扣动态width",player.position.X+player.width==server.Main.rightWorld-640 && player.velocity.X==0);
            check("右侧真实触边命中并支持向外按键",BoundaryContact.Side(player.position.X,player.width,server.Main.leftWorld,server.Main.rightWorld,2)==BoundarySide.Right && BoundaryContact.IsOutward(BoundarySide.Right,0,false,true));
            player.controlRight=false;
            check("朝外惯性无需按键也可触发",BoundaryContact.IsOutward(BoundarySide.Right,2,false,false));
            player.position=new(48*16,player.position.Y);
            check("旧48格位置不再提前触发",BoundaryContact.Side(player.position.X,player.width,server.Main.leftWorld,server.Main.rightWorld,2) is null);
            player.position=new(server.Main.leftWorld+640+3,player.position.Y);
            check("离实际边界三像素不算默认触边",BoundaryContact.Side(player.position.X,player.width,server.Main.leftWorld,server.Main.rightWorld,2) is null);
            player.position=new(server.Main.leftWorld+640+128,player.position.Y);
            check("重新武装必须身体完整退出八格边界带",!BoundaryContact.IsInterior(player.position.X,player.width,server.Main.leftWorld,server.Main.rightWorld));
        }
        finally
        {
            player.position=originalPosition; player.velocity=originalVelocity;
            player.controlLeft=oldLeft; player.controlRight=oldRight;
        }
    }
}
