﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioLink.Client.Utilities;
using Newtonsoft.Json;

namespace BioLink.Client.Extensibility {

    public class TransformationPipline {

        private List<IValueTransformer> _pipeline = new List<IValueTransformer>();

        public TransformationPipline() {
        }

        public String transform(String value, IRowSource row) {
            String transformed = value;
            foreach (IValueTransformer t in _pipeline) {
                transformed = t.Transform(transformed, row);
            }
            return transformed;
        }

        public void AddTransformer(IValueTransformer transform) {
            _pipeline.Add(transform);
        }

        public List<IValueTransformer> Transformers {
            get { return _pipeline; }
        }


        public override String ToString() {            
            List<String> l = new List<String>();            
            foreach (IValueTransformer t in _pipeline) {
                l.Add(t.Name);                
            }
            return l.Join(" → ");
        }

        public String GetState() {
            var state = new List<Dictionary<String, object>>();
            foreach (IValueTransformer t in _pipeline) {
                var config = t.GetConfiguration();
                var entry = new Dictionary<String, object>();
                entry.Add("key", t.Key);
                entry.Add("config", config);
                state.Add(entry);
            }

            var settings = new JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto };
            var json = JsonConvert.SerializeObject(state, Formatting.None, settings);
            var base64 = StringUtils.ToBase64String(json);
            return base64;
        }

        public static TransformationPipline BuildFromState(String base64) {
            var pipeline = new TransformationPipline();

            var json = StringUtils.FromBase64String(base64);
            var settings = new JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto };
            var state = JsonConvert.DeserializeObject<List<Dictionary<String, Object>>>(json,settings);
            state.ForEach((entry) => {
                var key = entry["key"] as String;
                var config = entry["config"];
                var transform = TransformFactory.CreateTransform(key, config);
                pipeline.AddTransformer(transform);                
            });

            return pipeline;
        }

    }
}
