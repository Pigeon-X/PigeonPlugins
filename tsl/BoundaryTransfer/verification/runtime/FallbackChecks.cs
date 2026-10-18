using BoundaryTransfer;
using Terraria;
using UnifierTSL.Servers;

static partial class Program
{
    static void FallbackChecks(Action<string,bool> check, Func<string,ServerContext> createWorld,
        Action<ServerContext,int,ushort> createFloor)
    {
        var settings=BoundarySettings.FromConfig(new() { InwardSearchTiles=0,VerticalSearchTiles=0 });
        var shore=createWorld("海岸兜底");
        var shoreSpawn=(shore.Main.spawnTileX,shore.Main.spawnTileY);
        createFloor(shore,200,0);
        for(int x=0;x<160;x++)
            for(int y=100;y<200;y++) shore.Main.tile[x,y].liquid=255;
        var map=new TerrariaLandingMap(shore);
        check("真实海水同高度近处搜索失败",!BoundaryPlanner.Find(map,BoundarySide.Left,180*16,20,42,settings).Success);
        var coast=Finish(new(map,BoundarySide.Left,180*16,20,42,settings),map);
        check("真实海岸可自动超出旧内部搜索范围找到干燥岸地",coast.Success && coast.Spot.X>160*16 && coast.Spot.FeetY==200);
        check("岸上兜底继续通过实际身体液体与碰撞校验",coast.Success && !Collision.SolidCollision(shore,new(coast.Spot.X,coast.Spot.Y),20,42) && !Collision.WetCollision(shore,new(coast.Spot.X,coast.Spot.Y),20,42,out _,out _));
        var underground=createWorld("海底干燥洞穴");
        var caveSpawn=(underground.Main.spawnTileX,underground.Main.spawnTileY);
        createFloor(underground,200,0); createFloor(underground,700,1);
        for(int x=0;x<underground.Main.maxTilesX;x++)
            for(int y=100;y<200;y++) underground.Main.tile[x,y].liquid=255;
        var caveMap=new TerrariaLandingMap(underground);
        var cave=Finish(new(caveMap,BoundarySide.Left,180*16,20,42,settings),caveMap);
        check("真实海底有水地面不选而选下方干燥洞穴",cave.Success && cave.Spot.FeetY==700);
        check("海底洞穴在最近边界列且有平整承重面",cave.Success && cave.Spot.X==96*16+8-10 && BoundaryPlanner.IsSafe(caveMap,cave.Spot,20,42,1));
        var sky=createWorld("高空落点"); var skySpawn=(sky.Main.spawnTileX,sky.Main.spawnTileY); createFloor(sky,800,0);
        var skyMap=new TerrariaLandingMap(sky);
        var landed=Finish(new(skyMap,BoundarySide.Right,80*16,20,42,settings),skyMap);
        check("高空原Y没有地面仍自动找到右边界下方安全地面",landed.Success && landed.Spot.FeetY==800 && landed.Spot.X>sky.Main.maxTilesX*8);
        var empty=createWorld("完全空白世界"); var emptyMap=new TerrariaLandingMap(empty);
        var none=Finish(new(emptyMap,BoundarySide.Left,300*16,20,42,settings),emptyMap);
        check("完全空白无承重面仍不得伪造安全成功",!none.Success && none.Candidates>0);
        check("兜底没有修改世界出生点",shoreSpawn==(shore.Main.spawnTileX,shore.Main.spawnTileY) && caveSpawn==(underground.Main.spawnTileX,underground.Main.spawnTileY) && skySpawn==(sky.Main.spawnTileX,sky.Main.spawnTileY));
    }

    static LandingResult Finish(BoundaryFallbackSearch search,ILandingMap map)
    {
        for(int i=0;i<10000;i++)
        {
            var step=search.Advance(map,16384);
            if(step.Complete) return step.Result;
        }
        throw new InvalidOperationException("兜底搜索未在有限地图内结束");
    }
}
