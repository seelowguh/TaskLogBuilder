using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Data;
using System.Windows.Forms.Layout;
using System.Xml;

namespace ClarifiLogBuilder
{
    public class Database
    {
        private string conn;
        public static string BuildConnectionString(string Server, string User, string Pass, string Database, bool TrustedConnection)
        {
            string ConnectionString = string.Empty;
            string Credentials = string.Empty;
            if (TrustedConnection)
                Credentials = "Trusted_Connection=True";
            else
                Credentials = string.Format("User ID={0};Password={1}", User, Pass);

            ConnectionString =
                String.Format(
                    "Data Source={0};Initial Catalog={1};Persist Security Info=True;{2}", Server,
                    Database, Credentials);
            
            return ConnectionString;
        }

        public Database(string ConnectionString)
        {
            conn = ConnectionString;
        }

        public string GetWFO(string ObjectID, string wfoID, ref string Type, ref string Path, ref string ObjectType, ref frmMain.ObjectDetails Od)
        {
            string Query =
                string.Format(
                    "SELECT TOP 1 WFO_ParentObjectID, WO.WFO_ID, WO.WFO_Name, WOE.Object_Type FROM tblWorkflowObjects WO JOIN tblWorkFlowObjectElements WOE ON WO.WFO_ID = WOE.WFO_ID WHERE Object_ID = REPLICATE('0', 10 - LEN('{0}')) + '{0}' AND WO.WFO_ID <> '{1}' ORDER BY WFO_Level DESC;",
                    ObjectID, wfoID);
            string nWFOID = string.Empty;
            try
            {
                using (SqlConnection sc = new SqlConnection(conn))
                {
                    sc.Open();
                    using (SqlCommand cmd = new SqlCommand(Query, sc))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                switch (dr.GetString(3))
                                {
                                    case "TransactionObject":
                                        ObjectType = "TO";
                                        Od.ObjectType = "TO";
                                        Od.ObjectName = "T" + ObjectID;
                                        break;
                                    case "PresentationObject":
                                        ObjectType = "PO";
                                        Od.ObjectType = "PO";
                                        Od.ObjectName = "P" + ObjectID;
                                        break;
                                    case "DatabaseObject":
                                        //  Check for Database object name
                                        Path += GetDatabaseObjectName(ObjectID);
                                        break;
                                }
                                nWFOID = dr.GetString(1);
                                if (Path != string.Empty)
                                    Path = "/" + Path;
                                Path = dr.GetString(2) + Path;
                                Type = dr.GetString(3);

                                if (nWFOID != string.Empty)
                                {
                                    GetWFO(dr.GetString(0), nWFOID, ref Type, ref Path, ref ObjectType, ref Od);
                                }
                            }
                            dr.Dispose();
                        }
                        cmd.Dispose();
                    }
                    sc.Close();
                }
            }
            catch (SqlException ex)
            {
                throw ex;
            }
            return nWFOID;
        }

        public string GetDatabaseObjectName(string ID)
        {
            string Query = string.Format("SELECT Name FROM [tblDatabaseObjects] DO where DO.id = REPLICATE('0', 10-{1}) + '{0}';", ID, ID.Length);
            string DatabaseObjectName = string.Empty;
            try
            {
                using (SqlConnection sc = new SqlConnection(conn))
                {
                    sc.Open();
                    using (SqlCommand cmd = new SqlCommand(Query, sc))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                DatabaseObjectName = dr.GetString(0);
                            }
                            dr.Dispose();
                        }
                        cmd.Dispose();
                    }
                    sc.Close();
                }
            }
            catch (SqlException ex)
            {
                throw ex;
            }
            return DatabaseObjectName;   
        }

        public string GetMessageStructure(string _MessageName)
        {
            string _Output = null;
            string _OpeningTag = null;
            try
            {
                string _sSql =
                    string.Format(
                        "SELECT MOEData, MOERelative FROM tblMessageObjectElements MOE JOIN tblMessageObjects MO ON MOE.ID = MO.ID WHERE MO.Name = '{0}' ORDER BY MOEKey ASC;",
                        _MessageName);
                using (SqlConnection sc = new SqlConnection(conn))
                {
                    sc.Open();
                    using (SqlCommand cmd = new SqlCommand(_sSql, sc))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string MOEData = string.Format("<M>{0}</M>", dr.GetString(0));
                                XmlDocument _xml = new XmlDocument();
                                _xml.LoadXml(MOEData);
                                XmlNodeList _XNodeList = _xml.SelectNodes("/M");
                                XmlNode _xNode = _XNodeList.Item(0);

                                if (dr.GetString(1).ToLower() == "root")
                                    _OpeningTag = _xNode["Dim"].InnerText;
                                else
                                    _Output += string.Format("<{0}></{0}>", _xNode["Dim"].InnerText);
                            }
                            _Output = string.Format("<{0}>{1}</{0}>", _OpeningTag, _Output);
                            _Output = string.Format("<{0}>{1}</{0}>", "Message", _Output);
                            dr.Dispose();
                        }
                        cmd.Dispose();
                    }
                    sc.Close();
                }
                return _Output;
            }
            catch (NullReferenceException ex)
            {
                throw ex;
            }
            catch (SqlException ex)
            {
                throw ex;
            }
            
        }



    }
}
