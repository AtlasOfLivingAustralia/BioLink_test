﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioLink.Client.Utilities;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using BioLink.Data.Model;

namespace BioLink.Data {

    public class XMLIOService : BioLinkService {

        public XMLIOService(User user) : base(user) { }

        public void ExportXML(List<int> taxonIds, XMLIOExportOptions options, IProgressObserver progress, Func<bool> isCancelledCallback) {

            try {
                if (progress != null) {
                    progress.ProgressStart("Counting total taxa to export...");
                }

                var exporter = new XMLExporter(User, taxonIds, options, progress, isCancelledCallback);

                exporter.Export();

            } finally {
                if (progress != null) {
                    progress.ProgressEnd("Export complete.");
                }
            }

        }

        public List<XMLIOMultimediaLink> GetExportMultimediaLinks(string category, int intraCatId) {
            var mapper = new GenericMapperBuilder<XMLIOMultimediaLink>().build();
            return StoredProcToList("spXMLExportMulimediaList", mapper, _P("vchrCategory", category), _P("intIntraCatID", intraCatId));
        }

        public XMLIOMultimedia GetMultimedia(int mediaId) {

            var mapper = new GenericMapperBuilder<XMLIOMultimedia>().Override(new BinaryConvertingMapper("imgMultimedia")).build();
            XMLIOMultimedia ret = null;
            StoredProcReaderFirst("spXMLExportMultimediaGet", (reader) => {
                ret = mapper.Map(reader);
            }, _P("intMultimediaID", mediaId));

            return ret;

        }

        public List<int> GetTaxaIdsForParent(int parentId) {
            var ret = new List<int>();
            StoredProcReaderForEach("spBiotaList", (reader) => {
                ret.Add((int)reader["TaxaID"]);
            }, _P("intParentID", parentId));
            return ret;
        }

    }

    class BinaryConvertingMapper : ConvertingMapper {
        public BinaryConvertingMapper(string columnName) : base(columnName, (x) => { return ((System.Data.SqlTypes.SqlBinary)x).Value; }) { }
    }

    class XMLExporter {

        private TextWriter _logWriter = null;

        private GUIDToIDCache _taxonList;
        private GUIDToIDCache _referenceList;
        private GUIDToIDCache _journalList;
        private GUIDToIDCache _multimediaList;
        private GUIDToIDCache _associateList;
        private GUIDToIDCache _unplacedTaxon;
        private GUIDToIDCache _regionList;
        private GUIDToIDCache _siteList;
        private GUIDToIDCache _siteVisitList;
        private GUIDToIDCache _materialList;
        private Func<bool> _isCancelled;
        private FieldToNameMappings _taxonMappings;
        private FieldToNameMappings _taxonALNMappings;
        private FieldToNameMappings _taxonGANMappings;
        private FieldToNameMappings _taxonSANMappings;
        private FieldToNameMappings _taxonSANTypeDataMappings;
        private FieldToNameMappings _commonNameMappings;
        private FieldToNameMappings _referenceMappings;
        private FieldToNameMappings _multimediaMappings;
        private FieldToNameMappings _journalMappings;
        private FieldToNameMappings _refLinkMappings;
        private FieldToNameMappings _distributionMappings;
        private FieldToNameMappings _multimediaLinkMappings;
        private FieldToNameMappings _notesMappings;
        private FieldToNameMappings _keywordMappings;
        private FieldToNameMappings _storageLocationMappings;
        private FieldToNameMappings _materialMappings;
        private FieldToNameMappings _IDHistoryMappings;
        private FieldToNameMappings _subPartMappings;
        private FieldToNameMappings _associateMappings;
        private FieldToNameMappings _curationEventMappings;
        private FieldToNameMappings _siteVisitMappings;
        private FieldToNameMappings _siteMappings;
        private FieldToNameMappings _regionMappings;
        private FieldToNameMappings _trapMappings;

        private XMLExportObject _xmlDoc;
        private int _itemTotal;

        private TaxaService TaxaService { get; set; }
        private SupportService SupportService { get; set; }

        public XMLExporter(User user, List<int> taxonIds, XMLIOExportOptions options, IProgressObserver progress, Func<bool> isCancelledCallback) {
            this.User = user;
            this.TaxonIDs = taxonIds;
            this.Options = options;
            this.ProgressObserver = progress;
            _isCancelled = isCancelledCallback;

            if (options.KeepLogFile) {
                string logfile = SystemUtils.ChangeExtension(options.Filename, "log");
                _logWriter = new StreamWriter(logfile, false);
            }

        }

        public void Export() {
            try {

                Init();

                ExportBiota();

                _xmlDoc.Save(Options.Filename);

            } finally {
                if (_logWriter != null) {
                    _logWriter.Close();
                    _logWriter.Dispose();
                }
            }
        }

        private void ExportBiota() {
            Log("Counting total taxa to export");

            var taxonMap = new Dictionary<int, Taxon>();
            foreach (int taxonId in TaxonIDs) {
                var taxon = TaxaService.GetTaxon(taxonId);
                taxonMap[taxonId] = taxon;
                Log("Counting children of Taxon '{0}' ({1})", taxon.TaxaFullName, taxonId);
                var itemCount = GetItemCount(taxonId);
                _itemTotal += itemCount;

                if (IsCancelled) {
                    return;
                }
            }

            var logMsg = string.Format("{0} items to export", _itemTotal);
            Log(logMsg);
            if (ProgressObserver != null) {
                ProgressObserver.ProgressMessage(logMsg);
            }
            StartTime = DateTime.Now;
            var XMLTaxaNode = _xmlDoc.TaxaRoot;
            foreach (int taxonId in TaxonIDs) {
                if (IsCancelled) {
                    break;
                }

                var taxon = taxonMap[taxonId];

                if (Options.IncludeFullClassification) {
                    XMLTaxaNode = ImportParents(XMLTaxaNode, taxon);
                }

                if (XMLTaxaNode != null) {
                    AddTaxonElement(XMLTaxaNode, taxon, Options.ExportChildTaxa);
                }

            }
        }

        private XmlElement ImportParents(XmlElement parent, Taxon taxon) {

            var newParent = parent;

            var strParentIds = taxon.Parentage.Split('\\');
            var parentIds = new List<int>();
            foreach (string s in strParentIds) {
                if (!string.IsNullOrEmpty(s)) {
                    parentIds.Add(Int32.Parse(s));
                }
            }



            foreach (int parentId in parentIds) {
                if (parentId == taxon.TaxaID.Value) {
                    break;
                } else {
                    var parentTaxon = TaxaService.GetTaxon(parentId);
                    newParent = AddTaxonElement(newParent, parentTaxon, false);
                }
            }

            return newParent;
        }

        private XmlElement AddTaxonElement(XmlElement parent, Taxon taxon, bool recurseChildren) {

            if (IsCancelled) {
                return null;
            }

            XmlElement taxonNode = null;

            var guid = _taxonList.GUIDForID(taxon.TaxaID.Value);
            if (guid != null) {
                if (recurseChildren) {
                    taxonNode = _xmlDoc.GetElementByGUID(guid, "TAXON");
                }

            } else {
                guid = taxon.GUID.Value.ToString();
                taxonNode = _xmlDoc.CreateNode(parent, "TAXON");
                taxonNode.AddAttribute("ID", guid);
                _taxonList[guid] = taxon.TaxaID.Value;

                CreateNamedNode(taxonNode, "vchrFullName", taxon.TaxaFullName);
                CreateNamedNode(taxonNode, "vchrEpithet", taxon.Epithet);
                CreateNamedNode(taxonNode, "vchrYearOfPub", taxon.YearOfPub);
                CreateNamedNode(taxonNode, "vchrAuthor", taxon.Author);
                CreateNamedNode(taxonNode, "vchrNameQualifier", taxon.NameStatus);

                // Rank and Kingom are special!
                var taxaService = new TaxaService(User);
                var rank = taxaService.GetTaxonRank(taxon.ElemType);
                var kingdomLong = taxaService.GetKingdomName(taxon.KingdomCode);

                CreateNamedNode(taxonNode, "RankLong", rank == null ? "" : rank.LongName);
                CreateNamedNode(taxonNode, "KingdomLong", kingdomLong);
                CreateNamedNode(taxonNode, "intOrder", taxon.Order);
                CreateNamedNode(taxonNode, "bitChangedComb", taxon.ChgComb);
                CreateNamedNode(taxonNode, "bitUnplaced", taxon.Unplaced);
                CreateNamedNode(taxonNode, "bitUnverified", taxon.Unverified);
                CreateNamedNode(taxonNode, "bitAvailableName", taxon.AvailableName);
                CreateNamedNode(taxonNode, "bitLiteratureName", taxon.LiteratureName);
                if (taxon.AvailableName.ValueOrFalse() || taxon.LiteratureName.ValueOrFalse()) {
                    AddAvailableNameData(taxonNode, taxon, rank);
                }

                CreateNamedNode(taxonNode, "txtDistQual", taxon.DistQual);
                CreateNamedNode(taxonNode, "dtDateCreated", FormatDate(taxon.DateCreated));
                CreateNamedNode(taxonNode, "vchrWhoCreated", taxon.WhoCreated);

                // CommonNames
                // AddCommonNames(taxonNode, taxon);
                // References
                //AddReferences TaxonNode, TaxonID, "Taxon"
                // Distribution
                //AddTaxonDistribution TaxonNode, TaxonID
                // Multimedia
                //AddMultimedia TaxonNode, TaxonID, "Taxon"
                // Morphology
                //AddMorphology TaxonNode, TaxonID, "Taxon"
                // Associates
                //AddTaxonAssociates TaxonNode, TaxonID
                // Traits
                //AddTraits TaxonNode, TaxonID, "Taxon"
                // Keywords
                //AddKeywords TaxonNode, TaxonID, "Taxon"
                // Notes
                //AddNotes TaxonNode, TaxonID, "Taxon"
                // Storage Locations
                //AddStorageLocations TaxonNode, TaxonID

                // Material
                if (Options.ExportMaterial) {
                    // AddMaterialForTaxon TaxonNode, TaxonID
                }

                // If an associate has placed this taxon in the UnplacedList, remove it (it is //placed// now!)
                if (_unplacedTaxon.ContainsValue(taxon.TaxaID.Value)) {
                    // RemoveFromUnplacedList TaxonID
                }
            }

            if (Options.ExportChildTaxa && !IsCancelled) {

                var childIds = new XMLIOService(User).GetTaxaIdsForParent(taxon.TaxaID.Value);
                
                foreach (int childId in childIds) {
                    if (IsCancelled) {
                        break;
                    }

                    var child = TaxaService.GetTaxon(childId);
                    AddTaxonElement(taxonNode, child, true);
                }
            }

            return taxonNode;
        }

        private void AddAvailableNameData(XmlElement taxonNode, Taxon taxon, TaxonRank rank) {
            Log("Exporting Available name data (TaxonID={0}) Rank Category='{1}'", taxon.TaxaID.Value, rank.Category);

            if (taxon.AvailableName.ValueOrFalse()) {
                switch (rank.Category.ToLower()) {
                    case "s":        // Species Available Name
                        AddSANData(taxonNode, taxon);
                        break;
                    case "g":        // Genus Available Name
                        AddGANData(taxonNode, taxon);
                        break;
                    case "h":
                    case "f": // Literature Available name
                        AddALNData(taxonNode, taxon);
                        break;
                    default:
                        Log("Unknown Rank Category (TaxonID={0}), RankCategory='{1}'). No Available Name Data exported.", taxon.TaxaID.Value, rank.Category);
                        break;
                }
            } else if (taxon.LiteratureName.ValueOrFalse()) {
                AddALNData(taxonNode, taxon);
            }
        }

        private void AddALNData(XmlElement taxonNode, Taxon taxon) {
            Log("Literature Available Name (TaxonID={0})", taxon.TaxaID.Value);
            var aln = TaxaService.GetAvailableName(taxon.TaxaID.Value);

            if (aln != null) {
                var XMLNode = _xmlDoc.CreateNode(taxonNode, "ALN");
                XMLNode.AddAttribute("ID", aln.GUID.Value.ToString());
                CreateNamedNode(XMLNode, "intRefID", AddReferenceItem(aln.RefID));
                CreateNamedNode(XMLNode, "vchrRefPage", aln.RefPage);
                CreateNamedNode(XMLNode, "txtRefQual", aln.RefQual);
                CreateNamedNode(XMLNode, "vchrRefCode", aln.RefCode);
            }
        }

        private string AddReferenceItem(int? refId) {
            if (refId.HasValue) {
                var guid = _referenceList.GUIDForID(refId.Value);
                if (!string.IsNullOrEmpty(guid)) {
                    var r = SupportService.GetReference(refId.Value);
                    if (r != null) {
                        Log("Adding Reference (RefID={0})", refId);
                        var XMLRefParent = _xmlDoc.ReferenceRoot;
                        var XMLRefNode = _xmlDoc.CreateNode(XMLRefParent, "REFERENCE");
                        guid = r.GUID.ToString();
                        XMLRefNode.AddAttribute("ID", guid);
                        CreateNamedNode(XMLRefNode, "vchrRefCode", r.RefCode);
                        CreateNamedNode(XMLRefNode, "vchrAuthor", r.Author);
                        CreateNamedNode(XMLRefNode, "vchrTitle", r.Title);
                        CreateNamedNode(XMLRefNode, "vchrBookTitle", r.BookTitle);
                        CreateNamedNode(XMLRefNode, "vchrEditor", r.Editor);
                        CreateNamedNode(XMLRefNode, "vchrRefType", r.RefType);
                        CreateNamedNode(XMLRefNode, "vchrYearOfPub", r.YearOfPub);
                        CreateNamedNode(XMLRefNode, "vchrActualDate", r.ActualDate);
                        CreateNamedNode(XMLRefNode, "vchrPartNo", r.PartNo);
                        CreateNamedNode(XMLRefNode, "vchrSeries", r.Series);
                        CreateNamedNode(XMLRefNode, "vchrPublisher", r.Publisher);
                        CreateNamedNode(XMLRefNode, "vchrPlace", r.Place);
                        CreateNamedNode(XMLRefNode, "vchrVolume", r.Volume);
                        CreateNamedNode(XMLRefNode, "vchrPages", r.Pages);
                        CreateNamedNode(XMLRefNode, "vchrTotalPages", r.TotalPages);
                        CreateNamedNode(XMLRefNode, "vchrPossess", r.Possess);
                        CreateNamedNode(XMLRefNode, "vchrSource", r.Source);
                        CreateNamedNode(XMLRefNode, "vchrEdition", r.Edition);
                        CreateNamedNode(XMLRefNode, "vchrISBN", r.ISBN);
                        CreateNamedNode(XMLRefNode, "vchrISSN", r.ISSN);
                        CreateNamedNode(XMLRefNode, "txtAbstract", r.Abstract);
                        CreateNamedNode(XMLRefNode, "txtFullText", r.FullText);
                        CreateNamedNode(XMLRefNode, "intStartPage", r.StartPage);
                        CreateNamedNode(XMLRefNode, "intEndPage", r.EndPage);
                        CreateNamedNode(XMLRefNode, "vchrJournalName", r.JournalName);
                        CreateNamedNode(XMLRefNode, "intJournalID", AddJournal(r.JournalID.HasValue ? r.JournalID.Value : -1));
                        CreateNamedNode(XMLRefNode, "dtDateCreated", FormatDate(r.DateCreated));
                        CreateNamedNode(XMLRefNode, "vchrWhoCreated", r.WhoCreated);

                        AddTraits(XMLRefNode, refId.Value, "Reference");

                        AddNotes(XMLRefNode, refId.Value, "Reference");

                        // AddKeywords(XMLRefNode, refId.Value, "Reference");

                        AddMultimedia(XMLRefNode, refId.Value, "Reference");

                        _referenceList.Add(guid, refId.Value);

                    } else {
                        Log("Failed to extract reference detail for ref id {0}", refId);
                    }
                }
                return guid;
            } else {
                return "";
            }
        }

        private string AddJournal(int journalId) {
            if (journalId < 0) {
                return "";
            }
        
            var guid = _journalList.GUIDForID(journalId);
            if (string.IsNullOrEmpty(guid)) {
                var journal = SupportService.GetJournal(journalId);
                Log("Adding Journal Item (JournalID={0})", journalId );
                var XMLJournal = _xmlDoc.CreateNode(_xmlDoc.JournalRoot, "JOURNAL");
                guid = journal.GUID.ToString();
                XMLJournal.AddAttribute("ID", guid);
                CreateNamedNode(XMLJournal, "vchrAbbrevName", journal.AbbrevName);
                CreateNamedNode(XMLJournal, "vchrAbbrevName2", journal.AbbrevName2);
                CreateNamedNode(XMLJournal, "vchrAlias", journal.Alias);
                CreateNamedNode(XMLJournal, "vchrFullName", journal.FullName);
                CreateNamedNode(XMLJournal, "txtNotes", ExpandNotes(journal.Notes));
                CreateNamedNode(XMLJournal, "dtDateCreated", FormatDate(journal.DateCreated));
                CreateNamedNode(XMLJournal, "vchrWhoCreated", journal.WhoCreated);
        
                AddTraits(XMLJournal, journalId, "Journal");
                AddNotes(XMLJournal, journalId, "Journal");        
                _journalList.Add(guid, journalId);
            }
    
            return guid;
        }

        private void AddMultimedia(XmlElement ParentNode, int itemId, string category) {

            if (!Options.ExportMultimedia) {
                return;
            }


            var links = new XMLIOService(User).GetExportMultimediaLinks(category, itemId);

            Log("Adding Multimedia ({0}, ID={1})", category, itemId);
            var XMLMM = _xmlDoc.CreateNode(ParentNode, "MULTIMEDIA");
            foreach (XMLIOMultimediaLink link in links) {
                if (IsCancelled) {
                    break;
                }
                var XMLMMNode = _xmlDoc.CreateNode(XMLMM, "MULTIMEDIALINK");
                XMLMMNode.AddAttribute("ID", link.GUID.ToString());
                CreateNamedNode(XMLMMNode, "intMultimediaID", AddMultimediaItem(link.MultimediaID));
                CreateNamedNode(XMLMMNode, "MultimediaType", link.MultimediaType);
                CreateNamedNode(XMLMMNode, "vchrCaption", link.Caption);
                CreateNamedNode(XMLMMNode, "bitUseInReport", link.UseInReport);
            }
        }

        private string AddMultimediaItem(int MediaID) {
            if (MediaID < 0) {
                return "";
            }

            var guid = _multimediaList.GUIDForID(MediaID);
            if (string.IsNullOrEmpty(guid)) {
                var mm = new XMLIOService(User).GetMultimedia(MediaID);
                Log("Adding Multimedia Item (MediaID={0})", MediaID);
                var XMLMM = _xmlDoc.CreateNode(_xmlDoc.MultimediaRoot, "MULTIMEDIAITEM");
                guid = mm.GUID.ToString();
                XMLMM.AddAttribute("ID", guid);
                XMLMM.AddAttribute("ENCODING", "UUENCODE");
                var strFilename = mm.Name;
                var strExtension = mm.FileExtension;
                CreateNamedNode(XMLMM, "vchrName", strFilename);
                CreateNamedNode(XMLMM, "vchrNumber", mm.Number);
                CreateNamedNode(XMLMM, "vchrArtist", mm.Artist);
                CreateNamedNode(XMLMM, "vchrDateRecorded", mm.DateRecorded);
                CreateNamedNode(XMLMM, "vchrOwner", mm.Owner);
                CreateNamedNode(XMLMM, "vchrFileExtension", strExtension);
                CreateNamedNode(XMLMM, "intSizeInBytes", mm.SizeInBytes);
                CreateNamedNode(XMLMM, "txtCopyright", mm.Copyright);
                CreateNamedNode(XMLMM, "dtDateCreated", FormatDate(mm.DateCreated));
                CreateNamedNode(XMLMM, "vchrWhoCreated", mm.WhoCreated);

                Log("UUEncoding image data for multimedia {0}", MediaID);

                using (var imgStream = new MemoryStream(mm.Multimedia)) {
                    using (var outputStream = new MemoryStream()) {
                        UUCodec.UUEncode(imgStream, outputStream);
                        outputStream.Position = 0;
                        var reader = new StreamReader(outputStream);
                        var imageData = reader.ReadToEnd();
                        var XMLData = _xmlDoc.XMLDocument.CreateCDataSection(imageData);
                        XMLMM.AppendChild(XMLData);

                    }
                }

                AddTraits(XMLMM, MediaID, "Multimedia");
                AddNotes(XMLMM, MediaID, "Multimedia");

                _multimediaList.Add(guid, MediaID);
            }
            return guid;
        }

        private void AddKeywords(XmlElement ParentNode, int itemId, string category) {
            throw new NotImplementedException();
        }

        private void AddNotes(XmlElement ParentNode, int itemId, string category) {

            if (!Options.ExportNotes) {
                return;
            }

            var notes = SupportService.GetNotes(category, itemId);

            Log("Adding Notes ({0}, ID={1})", category, itemId);
            var XMLNoteParent = _xmlDoc.CreateNode(ParentNode, "NOTES");
            foreach (Note note in notes) {
                if (IsCancelled) {
                    break;
                }
                var XMLNote = _xmlDoc.CreateNode(XMLNoteParent, "NOTEITEM");
                XMLNote.AddAttribute("ID", note.GUID.ToString());
                CreateNamedNode(XMLNote, "NoteType", note.NoteType);
                CreateNamedNode(XMLNote, "txtNote", ExpandNotes(note.NoteRTF));
                CreateNamedNode(XMLNote, "vchrAuthor", note.Author);
                CreateNamedNode(XMLNote, "txtComments", note.Comments);
                CreateNamedNode(XMLNote, "bitUseInReports", note.UseInReports);
                CreateNamedNode(XMLNote, "RefID", AddReferenceItem(note.RefID));
                CreateNamedNode(XMLNote, "vchrRefPages", note.RefPages);
            }
        }

        private string ExpandNotes(string notes) {
            var lngPos = notes.IndexOf("#");
            if (lngPos < 0) {
                return notes;
            }

            var bFinished = false;
            var strRemainder = notes.Substring(lngPos);
            while (!bFinished) {
                lngPos = strRemainder.IndexOf("#");
                if (lngPos < 0) {
                    bFinished = true;
                }
                var strBuf = strRemainder.Substring(0, lngPos);
                var refId = ValidateRefCode(strBuf);
                if (refId.HasValue) {
                    AddReferenceItem(refId);
                }

                strRemainder = notes.Substring(lngPos);
            }

            return notes;
        }

        private int? ValidateRefCode(string code) {
            if (code.Length > 50) {
                return null;
            }

            var lngPos = code.IndexOf(":");
            if (lngPos < 0) {
                return null;
            }
            var strCode = code.Substring(0, lngPos);

            return SupportService.GetReferenceIDFromRefCode(strCode);
        }

        private void AddTraits(XmlElement ParentNode, int itemId, string category) {
            if (!Options.ExportTraits) {
                return;
            }

            var traits = SupportService.GetTraits(category, itemId);
            Log("Adding Traits ({0}, ID={1}", category, itemId);

            foreach (Trait t in traits) {
                if (IsCancelled) {
                    break;
                }
                var alias = t.Name;
                var mangled = MangleName(alias);
                var XMLTraitItem = _xmlDoc.CreateNode(ParentNode, mangled);
                XMLTraitItem.AddAttribute("ALIAS", alias);
                XMLTraitItem.InnerText = t.Value;
            }
        }

        private string MangleName(string name) {
            var strReplace = " <>&:/\\" + (char)34;
            var sb = new StringBuilder();
            foreach (char ch in name) {
                if (strReplace.IndexOf(ch) >= 0) {
                    sb.Append("_");
                } else {
                    sb.Append(ch);
                }
            }
            return sb.ToString().ToUpper();
        }

        private void AddGANData(XmlElement taxonNode, Taxon taxon) {
            throw new NotImplementedException();
        }

        private void AddSANData(XmlElement taxonNode, Taxon taxon) {
            Log("Species Available Name (TaxonID={0})", taxon.TaxaID.Value);

            var san = TaxaService.GetSpeciesAvailableName(taxon.TaxaID.Value);
            var typeData = TaxaService.GetSANTypeData(taxon.TaxaID.Value);
            // var typeDataTypes = TaxaService.GetSANTypeDataTypes(taxon.TaxaID.Value);

            var XMLNode = _xmlDoc.CreateNode(taxonNode, "SAN");

            XMLNode.AddAttribute("ID", san.GUID.ToString());
            CreateNamedNode(XMLNode, "intRefID", AddReferenceItem(san.RefID));
            CreateNamedNode(XMLNode, "vchrRefPage", san.RefPage);
            CreateNamedNode(XMLNode, "txtRefQual", san.RefQual);
            CreateNamedNode(XMLNode, "vchrPrimaryType", san.PrimaryType);
            CreateNamedNode(XMLNode, "vchrSecondaryType", san.SecondaryType);
            CreateNamedNode(XMLNode, "bitPrimaryTypeProbable", san.PrimaryTypeProbable);
            CreateNamedNode(XMLNode, "bitSecondaryTypeProbable", san.SecondaryTypeProbable);
            CreateNamedNode(XMLNode, "vchrRefCode", san.RefCode);

            foreach (SANTypeData type in typeData) {
                var XMLTypeNode = _xmlDoc.CreateNode(XMLNode, "SANTYPE");

                XMLTypeNode.AddAttribute("ID", type.GUID.ToString());
                CreateNamedNode(XMLTypeNode, "vchrType", type.Type);
                CreateNamedNode(XMLTypeNode, "vchrMuseum", type.Museum);
                CreateNamedNode(XMLTypeNode, "vchrAccessionNum", type.AccessionNumber);
                CreateNamedNode(XMLTypeNode, "vchrMaterial", type.MaterialName);
                CreateNamedNode(XMLTypeNode, "vchrLocality", type.Locality);
                CreateNamedNode(XMLTypeNode, "bitIDConfirmed", type.IDConfirmed);
                if (Options.ExportMaterial) { // if I am exporting material then                
                    var lngMaterialID = type.MaterialID;
                    var strMaterialGUID = "";
                    if (lngMaterialID > 0) {
                        var XMLMaterialNode = AddMaterialItem(lngMaterialID, out strMaterialGUID);
                    }
                    CreateNamedNode(XMLTypeNode, "intMaterialID", strMaterialGUID);
                }
            }
    
        }

        private XmlElement AddMaterialItem(int? lngMaterialID, out string strMaterialGUID) {
            // TODO:
            strMaterialGUID = "";
            return null;
        }

        private string FormatDate(DateTime dt) {
            return string.Format("{0:yyyy-MM-dd}", dt);
        }

        private XmlElement CreateNamedNode(XmlElement parent, string fieldname, string value) {
            var strXMLTag = LookupXMLName(parent.Name, fieldname);
            return parent.AddNamedValue(strXMLTag, value);
        }

        private XmlElement CreateNamedNode(XmlElement parent, string fieldname, int? value) {
            var strXMLTag = LookupXMLName(parent.Name, fieldname);
            return parent.AddNamedValue(strXMLTag, value.HasValue ? value.Value + "" : "");
        }

        private XmlElement CreateNamedNode(XmlElement parent, string fieldname, bool? value) {
            var strXMLTag = LookupXMLName(parent.Name, fieldname);
            return parent.AddNamedValue(strXMLTag, value.ValueOrFalse() ? "1" : "0");
        }

        private string LookupXMLName(string category, string field) {
            var collection = GetCollectionForCategory(category);
            if (collection != null) {
                return collection[field].XMLName;
            }

            throw new Exception("Unrecognized category: " + category);
        }

        private FieldToNameMappings GetCollectionForCategory(string category) {
            switch (category.ToLower()) {
                case "taxon":
                    return _taxonMappings;
                case "aln":
                    return _taxonALNMappings;
                case "gan":
                    return _taxonGANMappings;
                case "san":
                    return _taxonSANMappings;
                case "santype":
                    return _taxonSANTypeDataMappings;
                case "commonname":
                    return _commonNameMappings;
                case "reference":
                    return _referenceMappings;
                case "multimediaitem":
                    return _multimediaMappings;
                case "journal":
                    return _journalMappings;
                case "multimedialink":
                    return _multimediaLinkMappings;
                case "referencelink":
                    return _refLinkMappings;
                case "distributionitem":
                    return _distributionMappings;
                case "noteitem":
                    return _notesMappings;
                case "keyworditem":
                    return _keywordMappings;
                case "storagelocation":
                    return _storageLocationMappings;
                case "material":
                    return _materialMappings;
                case "identification":
                    return _IDHistoryMappings;
                case "subpart":
                    return _subPartMappings;
                case "associate":
                    return _associateMappings;
                case "sitevisit":
                    return _siteVisitMappings;
                case "site":
                    return _siteMappings;
                case "region":
                    return _regionMappings;
                case "trap":
                    return _trapMappings;
                case "curationevent":
                    return _curationEventMappings;
                default:
                    return null;
            }

        }

        private int GetItemCount(int taxonId) {

            var stats = TaxaService.GetTaxonStatistics(taxonId);
            return stats.TotalItems + 1; // the taxon itself counts as one
        }

        private bool IsCancelled {
            get {
                if (_isCancelled != null) {
                    return _isCancelled();
                }

                return false;
            }
        }

        private void Init() {

            this.TaxaService = new TaxaService(User);
            this.SupportService = new SupportService(User);

            Log("Export XML started (User {0}, Database {1} on {2})", User.Username, User.ConnectionProfile.Database, User.ConnectionProfile.Server);
            Log("Destination file: {0}", Options.Filename);
            Log("Taxon IDS: {0}", TaxonIDs.Join(","));
            Log("Exporting child taxa: {0}", Options.ExportChildTaxa);
            Log("Exporting material: {0}", Options.ExportMaterial);
            Log("Exporting multimedia: {0}", Options.ExportMultimedia);
            Log("Exporting notes: {0}", Options.ExportNotes);
            Log("Exporting traits: {0}", Options.ExportTraits);
            Log("Including full classification: {0}", Options.IncludeFullClassification);

            _taxonList = new GUIDToIDCache();
            _referenceList = new GUIDToIDCache();
            _journalList = new GUIDToIDCache();
            _multimediaList = new GUIDToIDCache();
            _associateList = new GUIDToIDCache();
            _unplacedTaxon = new GUIDToIDCache();
            _regionList = new GUIDToIDCache();
            _unplacedTaxon = new GUIDToIDCache();
            _siteList = new GUIDToIDCache();
            _siteVisitList = new GUIDToIDCache();
            _materialList = new GUIDToIDCache();

            InitMappings();

            _xmlDoc = new XMLExportObject();
        }

        protected void InitMappings() {

            Log("Initializing field mappings...");

            // Taxon
            _taxonMappings = new FieldToNameMappings();

            _taxonMappings.Add("NAME", "vchrFullName");
            _taxonMappings.Add("EPITHET", "vchrEpithet");
            _taxonMappings.Add("PUBLICATIONYEAR", "vchrYearOfPub");
            _taxonMappings.Add("AUTHOR", "vchrAuthor");
            _taxonMappings.Add("NAMEQUALIFIER", "vchrNameQualifier");
            _taxonMappings.Add("ELEMENTTYPE", "chrElemType");
            _taxonMappings.Add("RANK", "RankLong", false);
            _taxonMappings.Add("KINGDOM", "KingdomLong", false);
            _taxonMappings.Add("ORDER", "intOrder");
            _taxonMappings.Add("ISCHANGEDCOMBINATION", "bitChangedComb");
            _taxonMappings.Add("ISUNPLACED", "bitUnplaced");
            _taxonMappings.Add("ISUNVERIFIED", "bitUnverified");
            _taxonMappings.Add("ISAVAILABLENAME", "bitAvailableName");
            _taxonMappings.Add("ISLITERATURENAME", "bitLiteratureName");
            _taxonMappings.Add("DISTRIBUTIONQUALIFICATION", "txtDistQual");
            _taxonMappings.Add("DATECREATED", "dtDateCreated");
            _taxonMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Taxon Literature Available Name (ALN)
            _taxonALNMappings = new FieldToNameMappings();
            _taxonALNMappings.Add("REFID", "intRefID", false);
            _taxonALNMappings.Add("REFPAGE", "vchrRefPage");
            _taxonALNMappings.Add("REFQUAL", "txtRefQual");
            // Taxon Genus Available Name (GAN)
            _taxonGANMappings = new FieldToNameMappings();
            _taxonGANMappings.Add("REFID", "intRefID", false);
            _taxonGANMappings.Add("REFPAGE", "vchrRefPage");
            _taxonGANMappings.Add("REFQUAL", "txtRefQual");
            _taxonGANMappings.Add("DESIGNATION", "sintDesignation", false);
            _taxonGANMappings.Add("FIXATIONMETHOD", "vchrTSFixationMethod");
            _taxonGANMappings.Add("TYPESPECIES", "vchrTypeSpecies");
            // Taxon Species Available Name (SAN)
            _taxonSANMappings = new FieldToNameMappings();
            _taxonSANMappings.Add("REFID", "intRefID", false);
            _taxonSANMappings.Add("REFPAGE", "vchrRefPage");
            _taxonSANMappings.Add("REFQUAL", "txtRefQual");
            _taxonSANMappings.Add("PRIMARYTYPE", "vchrPrimaryType");
            _taxonSANMappings.Add("PRIMARYTYPEPROBABLE", "bitPrimaryTypeProbable");
            _taxonSANMappings.Add("SECONDARYTYPE", "vchrSecondaryType");
            _taxonSANMappings.Add("SECONDARYTYPEPROBABLE", "bitSecondaryTypeProbable");
            // Taxon Species Available Name Type Data (SAN Type)
            _taxonSANTypeDataMappings = new FieldToNameMappings();
            _taxonSANTypeDataMappings.Add("TYPE", "vchrType");
            _taxonSANTypeDataMappings.Add("INSTITUTION", "vchrMuseum");
            _taxonSANTypeDataMappings.Add("ACCESSIONNUMBER", "vchrAccessionNum");
            _taxonSANTypeDataMappings.Add("MATERIAL", "vchrMaterial");
            _taxonSANTypeDataMappings.Add("LOCALITY", "vchrLocality");
            _taxonSANTypeDataMappings.Add("IDCONFIRMED", "bitIDConfirmed");
            _taxonSANTypeDataMappings.Add("MATERIALID", "intMaterialID", false);
            // Common Names
            _commonNameMappings = new FieldToNameMappings();
            _commonNameMappings.Add("NAME", "vchrCommonName");
            _commonNameMappings.Add("REFID", "intRefID");
            _commonNameMappings.Add("REFPAGE", "vchrRefPage");
            _commonNameMappings.Add("NOTES", "txtNotes");
            _commonNameMappings.Add("REFCODE", "vchrRefCode", false);
            // Reference
            _referenceMappings = new FieldToNameMappings();
            _referenceMappings.Add("REFCODE", "vchrRefCode");
            _referenceMappings.Add("AUTHOR", "vchrAuthor");
            _referenceMappings.Add("TITLE", "vchrTitle");
            _referenceMappings.Add("BOOKTITLE", "vchrBookTitle");
            _referenceMappings.Add("EDITOR", "vchrEditor");
            _referenceMappings.Add("REFTYPE", "vchrRefType");
            _referenceMappings.Add("PUBLICATIONYEAR", "vchrYearOfPub");
            _referenceMappings.Add("ACTUALDATE", "vchrActualDate");
            _referenceMappings.Add("PARTNUMBER", "vchrPartNo");
            _referenceMappings.Add("SERIES", "vchrSeries");
            _referenceMappings.Add("PUBLISHER", "vchrPublisher");
            _referenceMappings.Add("PLACE", "vchrPlace");
            _referenceMappings.Add("VOLUME", "vchrVolume");
            _referenceMappings.Add("PAGES", "vchrPages");
            _referenceMappings.Add("TOTALPAGES", "vchrTotalPages");
            _referenceMappings.Add("POSSESS", "vchrPossess");
            _referenceMappings.Add("SOURCE", "vchrSource");
            _referenceMappings.Add("EDITION", "vchrEdition");
            _referenceMappings.Add("ISBN", "vchrISBN");
            _referenceMappings.Add("ISSN", "vchrISSN");
            _referenceMappings.Add("ABSTRACT", "txtAbstract");
            _referenceMappings.Add("FULLTEXT", "txtFullText");
            _referenceMappings.Add("STARTPAGE", "intStartPage");
            _referenceMappings.Add("ENDPAGE", "intEndPage");
            _referenceMappings.Add("JOURNALNAME", "vchrJournalName", false);
            _referenceMappings.Add("JOURNALID", "intJournalID");
            _referenceMappings.Add("DATECREATED", "dtDateCreated");
            _referenceMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Multimedia Item
            _multimediaMappings = new FieldToNameMappings();
            _multimediaMappings.Add("NAME", "vchrName");
            _multimediaMappings.Add("EXTENSION", "vchrFileExtension");
            _multimediaMappings.Add("NUMBER", "vchrNumber");
            _multimediaMappings.Add("ARTIST", "vchrArtist");
            _multimediaMappings.Add("DATERECORDED", "vchrDateRecorded");
            _multimediaMappings.Add("OWNER", "vchrOwner");
            _multimediaMappings.Add("COPYRIGHT", "txtCopyright");
            _multimediaMappings.Add("SIZEINBYTES", "intSizeInBytes");
            _multimediaMappings.Add("DATECREATED", "dtDateCreated");
            _multimediaMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Journal Items
            _journalMappings = new FieldToNameMappings();
            _journalMappings.Add("ABBREVNAME", "vchrAbbrevName");
            _journalMappings.Add("ALTABBREVNAME", "vchrAbbrevName2");
            _journalMappings.Add("ALIAS", "vchrAlias");
            _journalMappings.Add("FULLNAME", "vchrFullName");
            _journalMappings.Add("JOURNALNOTES", "txtNotes");
            _journalMappings.Add("DATECREATED", "dtDateCreated");
            _journalMappings.Add("WHOCREATED", "vchrWhoCreated");
            // RefLinks
            _refLinkMappings = new FieldToNameMappings();
            _refLinkMappings.Add("REFID", "intRefID");
            _refLinkMappings.Add("REFLINKTYPE", "RefLink", false);
            _refLinkMappings.Add("REFPAGE", "vchrRefPage");
            _refLinkMappings.Add("REFQUAL", "txtRefQual");
            _refLinkMappings.Add("ORDER", "intOrder");
            _refLinkMappings.Add("USEINREPORTS", "bitUseInReport");
            // Taxon distribution
            _distributionMappings = new FieldToNameMappings();
            _distributionMappings.Add("INTRODUCED", "bitIntroduced");
            _distributionMappings.Add("UNCERTAIN", "bitUncertain");
            _distributionMappings.Add("THROUGHOUTREGION", "bitThroughoutRegion");
            _distributionMappings.Add("QUALIFICATION", "txtQual");
            _distributionMappings.Add("FULLPATH", "txtDistRegionFullPath", false);
            // Multimedia Link
            _multimediaLinkMappings = new FieldToNameMappings();
            _multimediaLinkMappings.Add("MULTIMEDIAID", "intMultimediaID", false);
            _multimediaLinkMappings.Add("MULTIMEDIATYPE", "MultimediaType", false);
            _multimediaLinkMappings.Add("CAPTION", "vchrCaption");
            _multimediaLinkMappings.Add("USEINREPORTS", "bitUseInReport");
            // Notes Mappings
            _notesMappings = new FieldToNameMappings();
            _notesMappings.Add("NOTETYPE", "NoteType", false);
            _notesMappings.Add("NOTE", "txtNote");
            _notesMappings.Add("AUTHOR", "vchrAuthor");
            _notesMappings.Add("COMMENTS", "txtComments");
            _notesMappings.Add("USEINREPORTS", "bitUseInReports");
            _notesMappings.Add("REFID", "RefID", false);
            _notesMappings.Add("REFPAGES", "vchrRefPages");
            // Keyword Mappings
            _keywordMappings = new FieldToNameMappings();
            _keywordMappings.Add("TYPE", "Keyword", false);
            _keywordMappings.Add("VALUE", "vchrValue");
            _keywordMappings.Add("USEINREPORTS", "bitUseInReport");
            _keywordMappings.Add("QUALIFICATION", "txtValueQual");
            // Storage Location Mappings
            _storageLocationMappings = new FieldToNameMappings();
            _storageLocationMappings.Add("STORAGELOCATION", "StorageLocation", false);
            _storageLocationMappings.Add("FULLPATH", "StoragePath", false);
            _storageLocationMappings.Add("NOTES", "txtNotes");
            // Material Mappings
            _materialMappings = new FieldToNameMappings();
            _materialMappings.Add("NAME", "vchrMaterialName");
            _materialMappings.Add("ACCESIONNUMBER", "vchrAccessionNo");
            _materialMappings.Add("REGISTRATIONNUMBER", "vchrRegNo");
            _materialMappings.Add("COLLECTORNUMBER", "vchrCollectorNo");
            _materialMappings.Add("IDENTIFIEDBY", "vchrIDBy");
            _materialMappings.Add("TAXONID", "intBiotaID");
            _materialMappings.Add("IDENTIFIEDDATE", "dtIDDate");
            _materialMappings.Add("REFID", "intIDRefID");
            _materialMappings.Add("TRAPID", "intTrapID");
            _materialMappings.Add("REFPAGE", "vchrIDRefPage");
            _materialMappings.Add("IDENTIFICATIONMETHOD", "vchrIDMethod");
            _materialMappings.Add("IDENTIFICATIONACCURACY", "vchrIDAccuracy");
            _materialMappings.Add("NAMEQUALIFICATION", "vchrIDNameQual");
            _materialMappings.Add("IDENTIFICATIONNOTES", "vchrIDNotes");
            _materialMappings.Add("INSTITUTION", "vchrInstitution");
            _materialMappings.Add("COLLECTIONMETHOD", "vchrCollectionMethod");
            _materialMappings.Add("ABUNDANCE", "vchrAbundance");
            _materialMappings.Add("MACROHABITAT", "vchrMacroHabitat");
            _materialMappings.Add("MICROHABITAT", "vchrMicroHabitat");
            _materialMappings.Add("SOURCE", "vchrSource");
            _materialMappings.Add("SPECIALLABEL", "vchrSpecialLabel");
            _materialMappings.Add("ORIGINALLABEL", "vchrOriginalLabel");
            _materialMappings.Add("DATECREATED", "dtDateCreated");
            _materialMappings.Add("WHOCREATED", "vchrWhoCreated");
            // IDHistory Mappings
            _IDHistoryMappings = new FieldToNameMappings();
            _IDHistoryMappings.Add("TAXON", "vchrTaxa");
            _IDHistoryMappings.Add("IDENTIFIEDBY", "vchr);IDBy");
            _IDHistoryMappings.Add("DATEIDENTIFIED", "dtIDDate");
            _IDHistoryMappings.Add("REFID", "intIDRefID");
            _IDHistoryMappings.Add("REFPAGE", "vchrIDRefPage");
            _IDHistoryMappings.Add("METHOD", "vchrIDMethod");
            _IDHistoryMappings.Add("ACCURACY", "vchrIDAccuracy");
            _IDHistoryMappings.Add("NAMEQUALIFICATION", "vchrNameQual");
            _IDHistoryMappings.Add("NOTES", "txtIDNotes");
            // Subpart Mappings
            _subPartMappings = new FieldToNameMappings();
            _subPartMappings.Add("NAME", "vchrPartName");
            _subPartMappings.Add("SAMPLETYPE", "vchrSampleType");
            _subPartMappings.Add("NUMBEROFSPECIMENS", "intNoSpecimens");
            _subPartMappings.Add("NUMBEROFSPECIMENSQUALIFCATION", "vchrNoSpecimensQual");
            _subPartMappings.Add("LIFESTAGE", "vchrLifeStage");
            _subPartMappings.Add("GENDER", "vchrGender");
            _subPartMappings.Add("REGISTRATIONNUMBER", "vchrRegNo");
            _subPartMappings.Add("CONDITION", "vchrCondition");
            _subPartMappings.Add("STORAGESITE", "vchrStorageSite");
            _subPartMappings.Add("STORAGEMETHOD", "vchrStorageMethod");
            _subPartMappings.Add("CURATIONSTATUS", "vchrCurationStatus");
            _subPartMappings.Add("NUMBEROFUNITS", "vchrNoOfUnits");
            _subPartMappings.Add("NOTES", "txtNotes");
            // Associate Mappings
            _associateMappings = new FieldToNameMappings();
            _associateMappings.Add("FROMCATEGORYID", "intFromCatID");
            _associateMappings.Add("FROMINTRACATID", "intFromIntraCatID");
            _associateMappings.Add("TOCATEGORYID", "intToCatID");
            _associateMappings.Add("TOINTRACATID", "intToIntraCatID");
            _associateMappings.Add("ASSOCDESCRIPTION", "txtAssocDescription");
            _associateMappings.Add("RELATIONFROMTO", "vchrRelationFromTo");
            _associateMappings.Add("RELATIONTOFROM", "vchrRelationToFrom");
            _associateMappings.Add("REGIONID", "intPoliticalRegionID");
            _associateMappings.Add("SOURCE", "vchrSource");
            _associateMappings.Add("REFID", "intRefID");
            _associateMappings.Add("REFPAGE", "vchrRefPage");
            _associateMappings.Add("UNCERTAIN", "bitUncertain");
            _associateMappings.Add("NOTES", "txtNotes");
            // Curation Event mappings
            _curationEventMappings = new FieldToNameMappings();
            _curationEventMappings.Add("SUBPARTNAME", "vchrSubpartName");
            _curationEventMappings.Add("WHO", "vchrWho");
            _curationEventMappings.Add("DATE", "dtWhen");
            _curationEventMappings.Add("EVENTTYPE", "vchrEventType");
            _curationEventMappings.Add("DESCRIPTION", "txtEventDesc");
            // SiteVisit Mappings
            _siteVisitMappings = new FieldToNameMappings();
            _siteVisitMappings.Add("NAME", "vchrSiteVisitName");
            _siteVisitMappings.Add("FIELDNUMBER", "vchrFieldNumber");
            _siteVisitMappings.Add("COLLECTOR", "vchrCollector");
            _siteVisitMappings.Add("DATESTART", "intDateStart", false);
            _siteVisitMappings.Add("DATEEND", "intDateEnd", false);
            _siteVisitMappings.Add("TIMESTART", "intTimeStart", false);
            _siteVisitMappings.Add("TIMEEND", "intTimeEnd", false);
            _siteVisitMappings.Add("CASUALTIME", "vchrCasualTime");
            _siteVisitMappings.Add("DATECREATED", "dtDateCreated");
            _siteVisitMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Site Mappings
            _siteMappings = new FieldToNameMappings();
            _siteMappings.Add("NAME", "vchrSiteName");
            _siteMappings.Add("LOCALITYTYPE", "tintLocalType", false);
            _siteMappings.Add("LOCALITY", "vchrLocal");
            _siteMappings.Add("DISTANCEFROMPLACE", "vchrDistanceFromPlace");
            _siteMappings.Add("DIRECTIONFROMPLACE", "vchrDirFromPlace");
            _siteMappings.Add("INFORMALLOCALITY", "vchrInformalLocal");
            _siteMappings.Add("COORDINATETYPE", "tintPosCoordinates", false);
            _siteMappings.Add("POSITIONGEOMETRY", "tintPosAreaType", false);
            _siteMappings.Add("X1", "fltPosX1");
            _siteMappings.Add("Y1", "fltPosY1");
            _siteMappings.Add("X2", "fltPosX2");
            _siteMappings.Add("Y2", "fltPosY2");
            _siteMappings.Add("POSITIONSOURCE", "vchrPosSource");
            _siteMappings.Add("POSITIONERROR", "vchrPosError");
            _siteMappings.Add("POSITIONWHO", "vchrPosWho");
            _siteMappings.Add("POSITIONDATE", "vchrPosDate");
            _siteMappings.Add("POSITIONORIGINAL", "vchrPosOriginal");
            _siteMappings.Add("POSITIONUTMSOURCE", "vchrPosUTMSource");
            _siteMappings.Add("POSITIONUTMPROJECTION", "vchrPosUTMMapProj");
            _siteMappings.Add("POSITIONUTMMAPNAME", "vchrPosUTMMapName");
            _siteMappings.Add("POSITIONUTMMAPVERSION", "vchrPosUTMMapVer");
            _siteMappings.Add("ELEVATIONTYPE", "tintElevType", false);
            _siteMappings.Add("ELEVATIONUPPER", "fltElevUpper");
            _siteMappings.Add("ELEVATIONLOWER", "fltElevLower");
            _siteMappings.Add("ELEVATIONDEPTH", "fltElevDepth");
            _siteMappings.Add("ELEVATIONUNITS", "vchrElevUnits");
            _siteMappings.Add("ELEVATIONSOURCE", "vchrElevSource");
            _siteMappings.Add("ELEVATIONERROR", "vchrElevError");
            _siteMappings.Add("GEOLOGYERA", "vchrGeoEra");
            _siteMappings.Add("GEOLOGYSTATE", "vchrGeoState");
            _siteMappings.Add("GEOLOGYPLATE", "vchrGeoPlate");
            _siteMappings.Add("GEOLOGYFORMATION", "vchrGeoFormation");
            _siteMappings.Add("GEOLOGYMEMBER", "vchrGeoMember");
            _siteMappings.Add("GEOLOGYBED", "vchrGeoBed");
            _siteMappings.Add("GEOLOGYNAME", "vchrGeoName");
            _siteMappings.Add("GEOLOGYAGEBOTTOM", "vchrGeoAgeBottom");
            _siteMappings.Add("GEOLOGYAGETOP", "vchrGeoAgeTop");
            _siteMappings.Add("GEOLOGYNOTES", "vchrGeoNotes");
            _siteMappings.Add("DATECREATED", "dtDateCreated");
            _siteMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Region Mappings
            _regionMappings = new FieldToNameMappings();
            _regionMappings.Add("NAME", "vchrName");
            _regionMappings.Add("RANK", "vchrRank");
            _regionMappings.Add("DATECREATED", "dtDateCreated");
            _regionMappings.Add("WHOCREATED", "vchrWhoCreated");
            // Trap Mappings
            _trapMappings = new FieldToNameMappings();
            _trapMappings.Add("NAME", "vchrTrapName");
            _trapMappings.Add("TYPE", "vchrTrapType");
            _trapMappings.Add("DESCRIPTION", "vchrDescription");
            _trapMappings.Add("DATECREATED", "dtDateCreated");
            _trapMappings.Add("WHOCREATED", "vchrWhoCreated");

        }

        protected void Log(string format, params object[] args) {
            if (_logWriter == null) {
                return;
            }

            var message = format;
            if (args.Length > 0) {
                message = string.Format(format, args);
            }

            _logWriter.WriteLine(string.Format("[{0:d/M/yyyy HH:mm:ss}] {1}", DateTime.Now, message));
        }

        protected User User { get; private set; }
        protected XMLIOExportOptions Options { get; private set; }
        protected IProgressObserver ProgressObserver { get; private set; }
        protected List<int> TaxonIDs { get; private set; }

        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
    }

    class GUIDToIDCache : Dictionary<string, int> {

        public string GUIDForID(int id) {
            foreach (string key in Keys) {
                if (this[key] == id) {
                    return key;
                }
            }
            return null;
        }
    }

    class FieldToNameMappings : Dictionary<string, NameMapping> {

        public void Add(string name, string field, bool isInsertable = true) {
            var mapping = new NameMapping { XMLName = name, FieldName = field, IsInsertable = isInsertable };
            Add(field, mapping);
        }

    }

    class NameMapping {
        public string XMLName { get; set; }
        public string FieldName { get; set; }
        public bool IsInsertable { get; set; }
    }

}