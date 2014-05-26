﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioLink.Client.Utilities;
using BioLink.Data;
using Ionic.Zip;
using System.IO;

namespace BioLink.Client.Extensibility {

    public class DarwinCoreArchiveExporter : TabularDataExporter {

        /// <summary>
        /// This exporter should only export columns that are Darwin core terms, so checks to see if there are any. If so, then yes, we can export them! 
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="datasetName"></param>
        /// <returns></returns>
        public override bool CanExport(Data.DataMatrix matrix, string datasetName) {
            Boolean hasDwCTerms = false;
            matrix.Columns.ForEach(column => {
                DarwinCoreField field;
                if (Enum.TryParse<DarwinCoreField>(column.Name, true, out field)) {
                    hasDwCTerms = true;
                }                
            });

            return hasDwCTerms;            
        }

        protected override object GetOptions(System.Windows.Window parentWindow, Data.DataMatrix matrix, string datasetName) {
            var filename = PromptForFilename(".zip", "DwC-A Files (.zip)|*.zip", datasetName);
            if (!String.IsNullOrEmpty(filename)) {

                if (FileExistsAndNotOverwrite(filename)) {
                    return null;
                }

                DarwinCoreExporterOptions options = new DarwinCoreExporterOptions();
                options.Filename = filename;
                return options;
            }

            return null;

        }

        public override void ExportImpl(System.Windows.Window parentWindow, Data.DataMatrix matrix, string datasetName, object options) {
            var opts = options as DarwinCoreExporterOptions;

            var columnNames = new List<String>();

            datasetName = SystemUtils.StripIllegalFilenameChars(datasetName);

            using (ZipFile archive = new ZipFile(opts.Filename)) {

                

                archive.AddEntry(String.Format("{0}\\occurrence.txt", datasetName), (String name, Stream stream) => {
                    ExportToCSV(matrix, stream, opts, true);
                });


                //archive.AddEntry(String.Format("{0}\\meta.xml", datasetName), (String name, Stream stream) => {
                //    WriteMetaXml(stream, opts, columnNames);
                //});

                archive.Save();
            }
        }

        private void WriteMetaXml(Stream stream, DarwinCoreExporterOptions options, List<String> columnNames) {
            using (var writer = new StreamWriter(stream)) {
                writer.WriteLine("<?xml version=\"1.0\"?>");
                writer.WriteLine("<archive xmlsn=\"http://rs.tdwg.org/dwc/text/\" metadata=\"eml.xml\">");
                writer.WriteLine("</archive>");
            }
        }

        private void ExportToCSV(DataMatrix matrix, Stream stream, DarwinCoreExporterOptions options, bool writeColumnHeaders) {
            // Now emit each row...
            int numCols = matrix.Columns.Count;
            var numRows = matrix.Rows.Count;
            var currentRow = 0;
            var _quote = '"';

            using (var writer = new StreamWriter(stream)) {
                if (writeColumnHeaders) {
                    for (int colIndex = 0; colIndex < numCols; ++colIndex) {
                        MatrixColumn col = matrix.Columns[colIndex];
                        if (!col.IsHidden) {
                            if (options.QuoteValues) {
                                writer.Write(_quote);
                            }
                            writer.Write(col.Name);
                            if (options.QuoteValues) {
                                writer.Write(_quote);
                            }
                            if (colIndex < numCols - 1) {
                                writer.Write(options.Delimiter);
                            }
                        }
                    }
                    writer.WriteLine();
                }

                for (int rowIndex = 0; rowIndex < matrix.Rows.Count; ++rowIndex) {
                    var row = matrix.Rows[rowIndex];
                    for (int colIndex = 0; colIndex < numCols; ++colIndex) {
                        if (!matrix.Columns[colIndex].IsHidden) {
                            var objValue = row[colIndex];
                            var value = objValue == null ? "" : objValue.ToString();

                            if (options.EscapeSpecial) {
                                value = value.Replace("\"", "\\\"");
                                value = value.Replace(options.Delimiter, "\\" + options.Delimiter);
                            }

                            var quoteValue = options.QuoteValues || value.Contains(options.Delimiter);

                            if (quoteValue) {
                                writer.Write(_quote);
                            }

                            writer.Write(value);

                            if (quoteValue) {
                                writer.Write(_quote);
                            }
                            if (colIndex < numCols - 1) {
                                writer.Write(options.Delimiter);
                            }
                        }
                    }
                    writer.WriteLine();
                    currentRow++;
                    if ((currentRow % 1000) == 0) {
                        double percent = (currentRow / ((double)numRows)) * 100.0;
                        ProgressMessage(String.Format("{0} rows exported to {1}", currentRow, options.Filename), percent);
                    }
                }
            }
        }
        
        public override void Dispose() {           
        }

        public override string Name {
            get { return "Darwin Core Archive"; }
        }

        public override string Description {
            get { return "Exports Darwin Core terms in DwC-A format (Data set must contain DwC terms)"; }
        }

        public override System.Windows.Media.Imaging.BitmapSource Icon {
            get { 
                return ImageCache.GetPackedImage("images/dwca_export.png", GetType().Assembly.GetName().Name);
            }
        }
    }

    class DarwinCoreExporterOptions {

        public DarwinCoreExporterOptions() {
            EscapeSpecial = true;
            Delimiter = ",";
            QuoteValues = true;
        }

        public String Filename { get; set; }
        public bool EscapeSpecial { get; set; }
        public String Delimiter { get; set; }
        public bool QuoteValues { get; set; }
    }

}
