using Newtonsoft.Json;
using System;
using TempoDB.Utility;


namespace TempoDB
{
    /// <summary>
    /// 
    /// </summary>
    public class Rollup
    {
        private Fold fold;
        private NodaTime.Period period;

        [JsonProperty(PropertyName="fold", Required=Required.Always)]
        public Fold Fold
        {
            get { return fold; }
            private set { this.fold = value; }
        }

        [JsonProperty(PropertyName="period", Required=Required.Always)]
        public NodaTime.Period Period
        {
            get { return period; }
            private set { this.period = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="period"></param>
        /// <param name="fold"></param>
        public Rollup(NodaTime.Period period, Fold fold)
        {
            this.period = period;
            this.fold = fold;
        }

        public override string ToString()
        {
            return string.Format("Rollup({0}, {1})", Fold, Period);
        }

        public override bool Equals(Object obj)
        {
            if(obj == null) { return false; }
            if(obj == this) { return true; }
            if(obj.GetType() != GetType()) { return false; }

            Rollup other = obj as Rollup;
            return new EqualsBuilder()
                .Append(Fold, other.Fold)
                .Append(Period, other.Period)
                .IsEquals();
        }

        public override int GetHashCode()
        {
            int hash = HashCodeHelper.Initialize();
            hash = HashCodeHelper.Hash(hash, Fold);
            hash = HashCodeHelper.Hash(hash, Period);
            return hash;
        }
    }
}
