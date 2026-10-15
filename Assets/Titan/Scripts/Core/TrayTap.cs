namespace Titan.Core
{
    /// <summary>
    /// A tap on a tray cannon (DESIGN.md 4.2.8, 11.4). The whole tray-tap path: it deploys or says why not, and has no
    /// way to open an ad card (it is given no governor, no offer, no ad service). GameController.HandleTap calls only
    /// this; EthicsLockTests check both.
    /// </summary>
    public static class TrayTap
    {
        public static DeployBlockReason Handle(TitanGame game, int traySlot)
        {
            var block = game.DeployBlock(traySlot);
            if (block == DeployBlockReason.None) game.Deploy(traySlot);
            return block;
        }
    }
}
