using System;
using System.Collections.Generic;

namespace medieval_game
{
    public class CharacterEffect
    {

    }

    public interface EntityAffliction
    {
        bool? IsPositive
        {
            get;
        }

        void Apply(BaseEntity entity);
        void Revoke(BaseEntity entity);
    }

    public class UndeadEntityAffliction : EntityAffliction
    {
        public bool? IsPositive
        {
            get
            {
                return null;
            }
        }

        public void Apply(BaseEntity entity)
        {
            entity.DamageCalculation += this.DamageCalculation;
            entity.HealCalculation += this.HealCalculation;
        }
        public void Revoke(BaseEntity entity)
        {
            entity.DamageCalculation -= this.DamageCalculation;
            entity.HealCalculation -= this.HealCalculation;
        }

        public void DamageCalculation(BaseEntity entity, EntityDamageEventArgs args)
        {
            if(args.Damage.Type == ElementalType.Demonic)
                args.AbsoluteValue *= -1;
        }
        public void HealCalculation(BaseEntity entity, EntityDamageEventArgs args)
        {
            if(args.Damage.Type != ElementalType.Demonic)
                args.AbsoluteValue *= -1;
        }
    }

    public class DoTEntityAffliction : EntityAffliction
    {
        public DoTEntityAffliction()
        {
            this.LastDamageMs = DateTime.Now.Ticks / 10000;
        }

        public bool? IsPositive
        {
            get
            {
                return false;
            }
        }

        private Damage Damage;
        private long LastDamageMs;
        private long IntervalMs;
        private long DurationMs;
        private long DurationLeftMs;

        public void Apply(BaseEntity entity)
        {
            entity.OnRuntime += this.Runtime;
        }
        public void Revoke(BaseEntity entity)
        {
            entity.OnRuntime -= this.Runtime;
        }

        private void Runtime(BaseEntity entity, RuntimeContext context)
        {
            this.DurationLeftMs -= context.LastRuntimeMs;
            this.LastDamageMs += context.LastRuntimeMs;
            if(this.DurationLeftMs < 0)
                this.LastDamageMs += this.DurationLeftMs;
            while(this.LastDamageMs >= this.IntervalMs)
            {
                entity.Heal(this.Damage);
                this.LastDamageMs -= this.IntervalMs;
            }
            if(this.DurationLeftMs <= 0)
                this.Revoke(entity);
        }
    }

    public enum ElementalType
    {
        Fire, Ice, Lightning,
        Divine, Demonic,
        Neutral,
        True // Cannot be smoothed
    }

    public enum RelativeAbsoluteValueRelativeness
    {
        Absolute, RelativeCurrent, RelativeMax
    }

    public class RelativeAbsoluteValue
    {
        public RelativeAbsoluteValueRelativeness Relativeness
        {
            get;
            set;
        }

        public int AbsoluteValue
        {
            get;
            set;
        }

        // (%)
        public float RelativeValue
        {
            get;
            set;
        }

        public int ToAbsolute(int current, int max)
        {
            switch(this.Relativeness)
            {
                default:
                case RelativeAbsoluteValueRelativeness.Absolute:
                    return this.AbsoluteValue;

                case RelativeAbsoluteValueRelativeness.RelativeCurrent:
                    return (int)(current * this.RelativeValue);

                case RelativeAbsoluteValueRelativeness.RelativeMax:
                    return (int)(max * this.RelativeValue);
            }
        }
    }

    public class Damage
    {
        public double Range
        {
            get;
            set;
        }

        public RelativeAbsoluteValue Value
        {
            get;
            set;
        }

        public ElementalType Type
        {
            get;
            set;
        }

        public RelativeAbsoluteValue Penetration
        {
            get;
            set;
        }
    }
    
    public class AffectableEntity : BaseEntity
    {
        public AffectableEntity()
        {
            this.Afflictions = new List<EntityAffliction>();
        }

        public List<EntityAffliction> Afflictions
        {
            get;
            set;
        }

        void Add(EntityAffliction affliction)
        {
            this.Afflictions.Add(affliction);
            affliction.Apply(this);
        }

        void Remove(EntityAffliction affliction)
        {
            this.Afflictions.Remove(affliction);
            affliction.Revoke(this);
        }
    }

    public class MaximizableValue
    {
        private int _Current;
        public int Current
        {
            get
            {
                return this._Current;
            }
            set
            {
                this._Current = Math.Max(0, Math.Min(value, this.Maximum));
            }
        }

        public int BaseMaximum
        {
            get;
            set;
        }

        public int Maximum
        {
            get;
            set;
        }
    }

    public class BaseEntity
    {
        public BaseEntity()
        { }

        public MaximizableValue HP
        {
            get;
            set;
        }

        public MaximizableValue Mana
        {
            get;
            set;
        }

        public event EntityDamageEvent DamageCalculation;
        public event EntityDamageEvent HealCalculation;
        public event EntityEvent OnDeath;

        public void Heal(Damage heal)
        {
            var eventArgs = new EntityDamageEventArgs()
            {
                AbsoluteValue = heal.Value.ToAbsolute(this.HP.Current, this.HP.Maximum),
                Damage = heal
            };

            if(eventArgs.AbsoluteValue == 0)
                return;

            bool isHeal = eventArgs.AbsoluteValue > 0;
            if(isHeal)
            {
                if(this.HealCalculation != null)
                    this.HealCalculation(this, eventArgs);
            }
            else
            {
                if(this.DamageCalculation != null)
                    this.DamageCalculation(this, eventArgs);
            }
                
            if(eventArgs.Cancel || eventArgs.AbsoluteValue == 0)
                return;
            
            if(isHeal && eventArgs.AbsoluteValue < 0)
            {
                if(this.DamageCalculation != null)
                    this.DamageCalculation(this, eventArgs);
                if(isHeal && eventArgs.AbsoluteValue > 0 || eventArgs.AbsoluteValue == 0)
                    return;
            }
            else if(!isHeal && eventArgs.AbsoluteValue > 0)
            {
                if(this.HealCalculation != null)
                    this.HealCalculation(this, eventArgs);
                if(!isHeal && eventArgs.AbsoluteValue < 0 || eventArgs.AbsoluteValue == 0)
                    return;
            }

            this.HP.Current += eventArgs.AbsoluteValue;
            if(this.IsDead && this.OnDeath != null)
                this.OnDeath(this);
        }

        public bool IsDead
        {
            get
            {
                return this.HP.Current == 0;
            }
        }

        public int Level
        {
            get;
            set;
        }

        public void LevelUp()
        {
            ++this.Level;
        }

        public void GainExperience(int value)
        {
            this.Experience += value;
        }

        public double ExperienceModifier
        {
            get;
            set;
        }

        private int _Experience;
        public int Experience
        {
            get
            {
                return this._Experience;
            }
            set
            {
                if(value >= this.MaximumExperience)
                {
                    this._Experience = value - this.MaximumExperience;
                    LevelUp();
                }
                else
                    this._Experience = value;
            }
        }

        public int MaximumExperience
        {
            get
            {
                return this.Level;
            }
        }

        public event EntityRuntimeEvent OnRuntime;
    }

    public class RuntimeContext
    {
        public long LastRuntimeMs;
    }

    public class EntityDamageEventArgs
    {
        public EntityDamageEventArgs()
        {
            this.Cancel = false;
        }

        public Damage Damage;
        public int AbsoluteValue;
        public bool Cancel;
    }

    public delegate void EntityRuntimeEvent(BaseEntity entity, RuntimeContext context);
    public delegate void EntityDamageEvent(BaseEntity entity, EntityDamageEventArgs args);
    public delegate void EntityEvent(BaseEntity entity);

    public class Character : BaseEntity
    {
    }
}
