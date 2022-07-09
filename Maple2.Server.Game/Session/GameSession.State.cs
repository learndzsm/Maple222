﻿using Maple2.Database.Model;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.Script.Npc;
using Maple2.Server.Game.Model;

namespace Maple2.Server.Game.Session;

public partial class GameSession {
    private const int ITEM_LOCK_MAX_STAGED_ITEMS = 18;
    public readonly long[] ItemLockStaging = new long[ITEM_LOCK_MAX_STAGED_ITEMS];

    private const int DISMANTLE_MAX_STAGED_ITEMS = 100;
    public bool DismantleOpened = false;
    public readonly (long Uid, int Amount)[] DismantleStaging = new (long, int)[DISMANTLE_MAX_STAGED_ITEMS];

    public Ride? Ride;

    public FieldGuideObject? GuideObject;
    public UgcItemCube HeldCube = UgcItemCube.Default;

    public ObjectWeapon? HeldLiftup;

    public NpcScript? NpcScript;
}
