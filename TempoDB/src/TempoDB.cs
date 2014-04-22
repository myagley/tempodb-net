using NodaTime;
using NodaTime.Text;
using RestSharp;
using System;
using System.Collections.Generic;
using TempoDB.Exceptions;
using TempoDB.Json;
using TempoDB.Utility;


namespace TempoDB
{
    /// <summary>
    /// 
    /// </summary>
    public class TempoDB
    {
        private Database database;
        private Credentials credentials;
        private string host;
        private int port;
        private bool secure;
        private string version;
        private RestClient client;

        private JsonSerializer serializer = new JsonSerializer();
        private string clientVersion = string.Format("tempodb-net/{0}", typeof(TempoDB).Assembly.GetName().Version.ToString());
        private const int DefaultTimeoutMillis = 50000;  // 50 seconds

        /// <summary>
        /// 
        /// </summary>
        /// <param name="database"></param>
        /// <param name="credentials"></param>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="version"></param>
        /// <param name="secure"></param>
        /// <param name="client"></param>
        public TempoDB(Database database, Credentials credentials, string host="api.tempo-db.com", int port=443, string version="v1", bool secure=true, RestClient client=null)
        {
            Database = database;
            Credentials = credentials;
            Host = host;
            Port = port;
            Version = version;
            Secure = secure;
            Client = client;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <returns></returns>
        public Response<Series> CreateSeries(Series series)
        {
            var url = "/{version}/series/";
            var request = BuildRequest(url, Method.POST, series);
            request.AddUrlSegment("version", Version);
            var response = Execute<Series>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="interval"></param>
        /// <returns></returns>
        public Response<Nothing> DeleteDataPoints(Series series, Interval interval)
        {
            var url = "/{version}/series/key/{key}/data/";
            var request = BuildRequest(url, Method.DELETE);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyIntervalToRequest(request, interval);
            var response = Execute<Nothing>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <returns></returns>
        public Response<Nothing> DeleteSeries(Series series)
        {
            var url = "/{version}/series/key/{key}/";
            var request = BuildRequest(url, Method.DELETE);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            var response = Execute<Nothing>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Response<DeleteSummary> DeleteSeries(Filter filter)
        {
            var url = "/{version}/series/";
            var request = BuildRequest(url, Method.DELETE);
            ApplyFilterToRequest(request, filter);
            var response = Execute<DeleteSummary>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Response<DeleteSummary> DeleteAllSeries()
        {
            var url = "/{version}/series/";
            var request = BuildRequest(url, Method.DELETE);
            request.AddParameter("allow_truncation", "true");
            var response = Execute<DeleteSummary>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="interval"></param>
        /// <param name="predicate"></param>
        /// <param name="zone"></param>
        /// <returns></returns>
        public Cursor<DataPointFound> FindDataPoints(Series series, Interval interval, Predicate predicate, DateTimeZone zone=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/series/key/{key}/find/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyIntervalToRequest(request, interval);
            ApplyTimeZoneToRequest(request, zone);
            ApplyPredicateToRequest(request, predicate);

            var response = Execute<DataPointFoundSegment>(request, typeof(DataPointFoundSegment));

            Cursor<DataPointFound> cursor = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<DataPointFound>(this, response.Value, typeof(DataPointFoundSegment));
                cursor = new Cursor<DataPointFound>(segments);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return cursor;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Response<Series> GetSeries(string key)
        {
            var url = "/{version}/series/key/{key}/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", key);
            var response = Execute<Series>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public Response<Cursor<Series>> GetSeries(Filter filter)
        {
            var url = "/{version}/series/";
            var request = BuildRequest(url, Method.GET);
            ApplyFilterToRequest(request, filter);
            var response = Execute<Segment<Series>>(request);

            Cursor<Series> cursor = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<Series>(this, response.Value, typeof(Segment<Series>));
                cursor = new Cursor<Series>(segments);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<Cursor<Series>>(cursor, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="interval"></param>
        /// <param name="zone"></param>
        /// <param name="rollup"></param>
        /// <param name="interpolation"></param>
        /// <returns></returns>
        public Response<QueryResult<DataPoint>> ReadDataPoints(Series series, Interval interval, DateTimeZone zone=null, Rollup rollup=null, Interpolation interpolation=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/series/key/{key}/data/segment/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyInterpolationToRequest(request, interpolation);
            ApplyIntervalToRequest(request, interval);
            ApplyTimeZoneToRequest(request, zone);
            ApplyRollupToRequest(request, rollup);

            var response = Execute<DataPointSegment>(request, typeof(DataPointSegment));

            QueryResult<DataPoint> query = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<DataPoint>(this, response.Value, typeof(DataPointSegment));
                var cursor = new Cursor<DataPoint>(segments);
                query = new QueryResult<DataPoint>(this, cursor, response.Value.Rollup);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<QueryResult<DataPoint>>(query, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="interval"></param>
        /// <param name="zone"></param>
        /// <param name="rollup"></param>
        /// <param name="interpolation"></param>
        /// <returns></returns>
        public Response<Cursor<MultiDataPoint>> ReadMultiRollupDataPoints(Series series, Interval interval, DateTimeZone zone, MultiRollup rollup, Interpolation interpolation=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/series/key/{key}/data/rollups/segment/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyIntervalToRequest(request, interval);
            ApplyInterpolationToRequest(request, interpolation);
            ApplyMultiRollupToRequest(request, rollup);
            ApplyTimeZoneToRequest(request, zone);

            var response = Execute<MultiRollupDataPointSegment>(request, typeof(MultiRollupDataPointSegment));

            Cursor<MultiDataPoint> cursor = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<MultiDataPoint>(this, response.Value, typeof(MultiRollupDataPointSegment));
                cursor = new Cursor<MultiDataPoint>(segments);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<Cursor<MultiDataPoint>>(cursor, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="interval"></param>
        /// <param name="aggregation"></param>
        /// <param name="zone"></param>
        /// <param name="rollup"></param>
        /// <param name="interpolation"></param>
        /// <returns></returns>
        public Response<QueryResult<DataPoint>> ReadDataPoints(Filter filter, Interval interval, Aggregation aggregation, DateTimeZone zone=null, Rollup rollup=null, Interpolation interpolation=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/segment/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            ApplyFilterToRequest(request, filter);
            ApplyInterpolationToRequest(request, interpolation);
            ApplyIntervalToRequest(request, interval);
            ApplyAggregationToRequest(request, aggregation);
            ApplyTimeZoneToRequest(request, zone);
            ApplyRollupToRequest(request, rollup);

            var response = Execute<DataPointSegment>(request, typeof(DataPointSegment));

            QueryResult<DataPoint> query = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<DataPoint>(this, response.Value, typeof(DataPointSegment));
                var cursor = new Cursor<DataPoint>(segments);
                query = new QueryResult<DataPoint>(this, cursor, response.Value.Rollup);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<QueryResult<DataPoint>>(query, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="interval"></param>
        /// <param name="zone"></param>
        /// <param name="rollup"></param>
        /// <param name="interpolation"></param>
        /// <returns></returns>
        public Response<QueryResult<MultiDataPoint>> ReadMultiDataPoints(Filter filter, Interval interval, DateTimeZone zone=null, Rollup rollup=null, Interpolation interpolation=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/multi/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            ApplyFilterToRequest(request, filter);
            ApplyInterpolationToRequest(request, interpolation);
            ApplyIntervalToRequest(request, interval);
            ApplyTimeZoneToRequest(request, zone);
            ApplyRollupToRequest(request, rollup);

            var response = Execute<MultiDataPointSegment>(request, typeof(MultiDataPointSegment));

            QueryResult<MultiDataPoint> query = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<MultiDataPoint>(this, response.Value, typeof(MultiDataPointSegment));
                var cursor = new Cursor<MultiDataPoint>(segments);
                query = new QueryResult<MultiDataPoint>(this, cursor, response.Value.Rollup);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<QueryResult<MultiDataPoint>>(query, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="timestamp"></param>
        /// <param name="zone"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public Response<SingleValue> ReadSingleValue(Series series, ZonedDateTime timestamp, DateTimeZone zone=null, Direction direction=Direction.Exact)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/series/key/{key}/single/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyDirectionToRequest(request, direction);
            ApplyTimestampToRequest(request, timestamp);
            ApplyTimeZoneToRequest(request, zone);
            var response = Execute<SingleValue>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="timestamp"></param>
        /// <param name="zone"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public Response<Cursor<SingleValue>> ReadSingleValue(Filter filter, ZonedDateTime timestamp, DateTimeZone zone=null, Direction direction=Direction.Exact)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/single/";
            var request = BuildRequest(url, Method.GET);
            ApplyFilterToRequest(request, filter);
            ApplyDirectionToRequest(request, direction);
            ApplyTimestampToRequest(request, timestamp);
            ApplyTimeZoneToRequest(request, zone);
            var response = Execute<Segment<SingleValue>>(request);

            Cursor<SingleValue> cursor = null;
            if(response.State == State.Success)
            {
                var segments = new SegmentEnumerator<SingleValue>(this, response.Value, typeof(Segment<SingleValue>));
                cursor = new Cursor<SingleValue>(segments);
            }
            else
            {
                throw new TempoDBException(string.Format("API Error: {0} - {1}", response.Code, response.Message));
            }
            return new Response<Cursor<SingleValue>>(cursor, response.Code, response.Message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="interval"></param>
        /// <param name="zone"></param>
        /// <returns></returns>
        public Response<Summary> ReadSummary(Series series, Interval interval, DateTimeZone zone=null)
        {
            if(zone == null) zone = DateTimeZone.Utc;
            var url = "/{version}/series/key/{key}/summary/";
            var request = BuildRequest(url, Method.GET);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            ApplyIntervalToRequest(request, interval);
            ApplyTimeZoneToRequest(request, zone);
            var response = Execute<Summary>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <returns></returns>
        public Response<Series> UpdateSeries(Series series)
        {
            var url = "/{version}/series/key/{key}/";
            var request = BuildRequest(url, Method.PUT, series);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            var response = Execute<Series>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Response<Nothing> WriteDataPoints(Series series, IList<DataPoint> data)
        {
            var url = "/{version}/series/key/{key}/data/";
            var request = BuildRequest(url, Method.POST, data);
            request.AddUrlSegment("version", Version);
            request.AddUrlSegment("key", series.Key);
            var response = Execute<Nothing>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writerequest"></param>
        /// <returns></returns>
        public Response<Nothing> WriteDataPoints(WriteRequest writerequest)
        {
            var url = "/{version}/multi/";
            var request = BuildRequest(url, Method.POST, writerequest);
            request.AddUrlSegment("version", Version);
            var response = Execute<Nothing>(request);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request"></param>
        /// <returns></returns>
        internal Response<T> Execute<T>(RestRequest request) where T : Model
        {
            return Execute<T>(request, typeof(T));
        }

        internal Response<T> Execute<T>(RestRequest request, Type type) where T : Model
        {
            var response = new Response<T>(Client.Execute(request), type);
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="method"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        internal RestRequest BuildRequest(string url, Method method, object body=null)
        {
            var request = new RestRequest {
                Method = method,
                Resource = url,
                Timeout = DefaultTimeoutMillis,
                RequestFormat = DataFormat.Json,
                JsonSerializer = serializer
            };
            request.AddHeader("Accept-Encoding", "gzip,deflate");
            request.AddHeader("User-Agent", clientVersion);

            if(body != null)
            {
                request.AddBody(body);
            }
            return request;
        }

        private static void ApplyAggregationToRequest(IRestRequest request, Aggregation aggregation)
        {
            if(aggregation != null)
            {
                request.AddParameter("aggregation.fold", aggregation.Fold.ToString().ToLower());
            }
        }

        private static void ApplyDirectionToRequest(IRestRequest request, Direction direction)
        {
            request.AddParameter("direction", direction.ToString().ToLower());
        }

        private static void ApplyFilterToRequest(IRestRequest request, Filter filter)
        {
            if(filter != null)
            {
                foreach(string key in filter.Keys)
                {
                    request.AddParameter("key", key);
                }
                foreach(string tag in filter.Tags)
                {
                    request.AddParameter("tag", tag);
                }
                foreach(var attribute in filter.Attributes)
                {
                    request.AddParameter(string.Format("attr[{0}]", attribute.Key), attribute.Value);
                }
            }
        }

        private static void ApplyInterpolationToRequest(IRestRequest request, Interpolation interpolation)
        {
            if(interpolation != null)
            {
                request.AddParameter("interpolation.period", PeriodPattern.NormalizingIsoPattern.Format(interpolation.Period));
                request.AddParameter("interpolation.function", interpolation.Function.ToString().ToLower());
            }
        }

        private static void ApplyIntervalToRequest(IRestRequest request, Interval interval)
        {
            var zone = DateTimeZone.Utc;
            request.AddParameter("start", ZonedDateTimeConverter.ToString(interval.Start.InZone(zone)));
            request.AddParameter("end", ZonedDateTimeConverter.ToString(interval.End.InZone(zone)));
        }

        private static void ApplyMultiRollupToRequest(IRestRequest request, MultiRollup rollup)
        {
            if(rollup != null)
            {
                foreach(Fold fold in rollup.Folds)
                {
                    request.AddParameter("rollup.fold", fold.ToString().ToLower());
                }
                request.AddParameter("rollup.period", PeriodPattern.NormalizingIsoPattern.Format(rollup.Period));
            }
        }

        private static void ApplyPredicateToRequest(IRestRequest request, Predicate predicate)
        {
            if(predicate != null)
            {
                request.AddParameter("predicate.period", PeriodPattern.NormalizingIsoPattern.Format(predicate.Period));
                request.AddParameter("predicate.function", predicate.Function.ToLower());
            }
        }

        private static void ApplyRollupToRequest(IRestRequest request, Rollup rollup)
        {
            if(rollup != null)
            {
                request.AddParameter("rollup.period", PeriodPattern.NormalizingIsoPattern.Format(rollup.Period));
                request.AddParameter("rollup.fold", rollup.Fold.ToString().ToLower());
            }
        }

        private static void ApplyTimestampToRequest(IRestRequest request, ZonedDateTime timestamp)
        {
            request.AddParameter("ts", ZonedDateTimeConverter.ToString(timestamp));
        }

        private static void ApplyTimeZoneToRequest(IRestRequest request, DateTimeZone zone)
        {
            var tz = zone == null ? DateTimeZone.Utc : zone;
            request.AddParameter("tz", tz.Id);
        }

        public Database Database
        {
            get { return this.database; }
            private set { this.database = value; }
        }

        public Credentials Credentials
        {
            get { return this.credentials; }
            private set { this.credentials = value; }
        }

        public string Host
        {
            get { return this.host; }
            private set { this.host = value; }
        }

        public int Port
        {
            get { return this.port; }
            private set { this.port = value; }
        }

        public string Version
        {
            get { return this.version; }
            private set { this.version = value; }
        }

        public bool Secure
        {
            get { return this.secure; }
            private set { this.secure = value; }
        }

        public RestClient Client
        {
            get
            {
                if(this.client == null)
                {
                    string protocol = Secure ? "https://" : "http://";
                    string portString = Port == 80 ? "" : ":" + Port;
                    string baseUrl = protocol + Host + portString;

                    var client = new RestClient {
                        BaseUrl = baseUrl,
                        Authenticator = new HttpBasicAuthenticator(Credentials.Key, Credentials.Secret)
                    };
                    Client = client;
                }
                return this.client;
            }
            private set { this.client = value; }
        }
    }
}
