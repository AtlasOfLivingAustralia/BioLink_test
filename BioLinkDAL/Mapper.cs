﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BioLink.Data.Model;
using System.Data.SqlClient;
using System.Data.Common;
using System.Reflection;
using BioLink.Client.Utilities;

namespace BioLink.Data {

    public abstract class MapperBase {

        private static Regex PREFIX_REGEX = new Regex("^([a-z]+)[A-Za-z]+$");

        private static string KNOWN_TYPE_PREFIXES = "chr,vchr,bit,int,txt,flt,tint,dt";

        public static void ReflectMap(object dest, DbDataReader reader, params string[] ignore) {
            PropertyInfo[] props = dest.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Dictionary<string, PropertyInfo> propMap = new Dictionary<string, PropertyInfo>();
            foreach (PropertyInfo propInfo in props) {
                if (propInfo.CanWrite) {
                    propMap.Add(propInfo.Name, propInfo);
                }
            }

            HashSet<string> ignoredNames = new HashSet<string>();
            foreach (string name in ignore) {
                ignoredNames.Add(name);
            }

            for (int i = 0; i < reader.FieldCount; ++i) {
                string name = reader.GetName(i);

                if (ignoredNames.Contains(name)) {
                    continue;
                }

                PropertyInfo target = null;

                if (propMap.ContainsKey(name)) {
                    target = propMap[name];
                } else {
                    // It may be that the column names have been prefixed with their database type.
                    // Look for common prefixes, and if so, strip the prefix off, and try that
                    Match m = PREFIX_REGEX.Match(name);
                    if (m.Success) {
                        string prefix = m.Groups[1].Value;

                        if (KNOWN_TYPE_PREFIXES.IndexOf(prefix) >= 0) {
                            string shortened = name.Substring(prefix.Length);
                            if (propMap.ContainsKey(shortened)) {
                                target = propMap[shortened];
                            }
                        }
                    }
                }

                if (target != null) {
                    object val = reader[i];
                    if (val is DBNull) {
                        val = null;
                    } else if (val is string) {
                        val = (val as string).TrimEnd();
                    }
                    target.SetValue(dest, val, null);
                } else {
                    Logger.Debug("Could not map field '{0}' to object of type {1}", name, dest.GetType().Name);
                }
            }
        }

    }

    public class TaxonMapper : MapperBase {

        public static Taxon MapTaxon(SqlDataReader reader) {
            Taxon t = new Taxon();
            ReflectMap(t, reader);
            return t;
        }

        public static TaxonSearchResult MapTaxonSearchResult(SqlDataReader reader) {
            TaxonSearchResult t = new TaxonSearchResult();
            ReflectMap(t, reader);
            return t;
        }

        public static TaxonRank MapTaxonRank(SqlDataReader reader) {
            TaxonRank tr = new TaxonRank();
            ReflectMap(tr, reader);
            return tr;
        }

    }


}