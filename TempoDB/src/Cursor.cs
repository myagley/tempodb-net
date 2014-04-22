using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using TempoDB.Exceptions;
using TempoDB.Utility;


namespace TempoDB
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Cursor<T> : Model where T: Model
    {
        private SegmentEnumerator<T> segments;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="segments"></param>
        public Cursor(SegmentEnumerator<T> segments)
        {
            this.segments = segments;
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach(Segment<T> segment in segments)
            {
                foreach(T item in segment)
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SegmentEnumerator<T> where T: Model
    {
        private Segment<T> segment;
        private TempoDB client;
        Type type;

        public SegmentEnumerator(TempoDB client, Segment<T> initial, Type type)
        {
            this.client = client;
            this.segment = initial;
            this.type = type;
        }

        public IEnumerator<Segment<T>> GetEnumerator()
        {
            yield return segment;
            while(String.IsNullOrEmpty(segment.NextUrl) == false)
            {
                // Add rest call here
                var request = client.BuildRequest(segment.NextUrl, Method.GET);
                var result = client.Execute<Segment<T>>(request, type);
                if(result.State == State.Success)
                {
                    segment = result.Value;
                    yield return segment;
                }
                else
                {
                    throw new TempoDBException(string.Format("API Error: {0} - {1}", result.Code, result.Message));
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Segment<T> : Model where T: Model
    {
        private IList<T> data;
        private string next;

        [JsonIgnore]
        public string NextUrl
        {
            get { return next; }
            set { this.next = value; }
        }

        [JsonProperty(PropertyName="data")]
        public IList<T> Data
        {
            get { return data; }
            protected set { this.data = value; }
        }

        public Segment(IList<T> data, string next)
        {
            this.data = data;
            this.next = next;
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach(T item in data)
            {
                yield return item;
            }
        }
    }
}
