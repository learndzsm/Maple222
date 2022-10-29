﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Maple2.Model.Enum;
using Maple2.Model.Metadata;
using Maple2.Server.Game.Manager.Field;
using Maple2.Server.Game.Model.Skill;
using Maple2.Server.Game.Packets;
using Maple2.Tools.Collision;
using Maple2.Tools.Scheduler;
using Serilog;

namespace Maple2.Server.Game.Model;

public abstract class ActorBase<T> : IActor<T> {
    private int idCounter;

    /// <summary>
    /// Generates an ObjectId unique to this specific actor instance.
    /// </summary>
    /// <returns>Returns a local ObjectId</returns>
    protected int NextLocalId() => Interlocked.Increment(ref idCounter);

    protected readonly ILogger Logger = Log.ForContext<T>();

    public FieldManager Field { get; }
    public T Value { get; }

    public virtual ConcurrentDictionary<int, Buff> Buffs => IActor.NoBuffs;
    public virtual Stats Stats { get; } = new(0, 0);

    public int ObjectId { get; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }

    public virtual bool IsDead { get; protected set; }
    public abstract IPrism Shape { get; }
    public virtual ActorState State { get; set; }
    public virtual ActorSubState SubState { get; set; }

    protected ActorBase(FieldManager field, int objectId, T value) {
        Field = field;
        ObjectId = objectId;
        Value = value;
    }

    public virtual void ApplyEffect(IActor caster, SkillEffectMetadata effect) { }
    public virtual void ApplyDamage(IActor caster, DamageRecord damage, SkillMetadataAttack attack) { }
    public virtual void AddBuff(IActor caster, int id, short level, bool notifyField = true) { }

    public virtual void Sync() { }
}

/// <summary>
/// Actor is an ActorBase that can engage in combat.
/// </summary>
/// <typeparam name="T">The type contained by this object</typeparam>
public abstract class Actor<T> : ActorBase<T>, IDisposable {
    protected readonly EventQueue Scheduler;

    public override ConcurrentDictionary<int, Buff> Buffs { get; } = new();

    protected Actor(FieldManager field, int objectId, T value) : base(field, objectId, value) {
        Scheduler = new EventQueue();
    }

    public override void ApplyEffect(IActor caster, SkillEffectMetadata effect) {
        Debug.Assert(effect.Condition != null);

        foreach (SkillEffectMetadata.Skill skill in effect.Skills) {
            AddBuff(caster, skill.Id, skill.Level);
        }
    }

    public override void ApplyDamage(IActor caster, DamageRecord damage, SkillMetadataAttack attack) {
        if (attack.Damage.Count > 0) {
            var targetRecord = new DamageRecordTarget {
                ObjectId = ObjectId,
                Position = caster.Position,
                Direction = caster.Rotation, // Idk why this is wrong
            };

            long damageAmount = 0;
            for (int i = 0; i < attack.Damage.Count; i++) {
                targetRecord.AddDamage(DamageType.Normal, -2000);
                damageAmount -= 2000;
            }

            if (damageAmount != 0) {
                Stats[StatAttribute.Health].Add(damageAmount);
                Field.Broadcast(StatsPacket.Update(this, StatAttribute.Health));
            }

            damage.Targets.Add(targetRecord);
        }
    }

    public override void AddBuff(IActor caster, int id, short level, bool notifyField = true) {
        if (Buffs.TryGetValue(id, out Buff? existing)) {
            existing.Stack();
            if (notifyField) {
                Field.Broadcast(BuffPacket.Update(existing));
            }
            return;
        }

        if (!Field.SkillMetadata.TryGetEffect(id, level, out AdditionalEffectMetadata? additionalEffect)) {
            Logger.Error("Invalid buff: {SkillId},{Level}", id, level);
            return;
        }

        var buff = new Buff(Field, additionalEffect, NextLocalId(), caster, this);
        if (!Buffs.TryAdd(id, buff)) {
            Logger.Error("Buff already exists: {SkillId}", id);
            return;
        }

        Logger.Information("{Id} AddBuff to {ObjectId}: {SkillId},{Level} for {Tick}ms", buff.ObjectId, ObjectId, id, level, buff.EndTick - buff.StartTick);
        // Logger.Information("> {Data}", additionalEffect.Property);
        if (notifyField) {
            Field.Broadcast(BuffPacket.Add(buff));
        }
    }

    public override void Sync() {
        Scheduler.InvokeAll();

        if (IsDead) {
            return;
        }

        if (Stats[StatAttribute.Health].Current <= 0) {
            IsDead = true;
            OnDeath();
            return;
        }

        foreach (Buff buff in Buffs.Values) {
            buff.Sync();
        }
    }

    public virtual void Dispose() {
        Scheduler.Stop();
        GC.SuppressFinalize(this);
    }

    protected abstract void OnDeath();
}
