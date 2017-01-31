using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Windows.Forms;

namespace ClarifiLogBuilder.Addon.SqlFunctions
{
    public partial class frmTableDataTransfer : Form
    {
        public frmTableDataTransfer()
        {
            InitializeComponent();
        }

        private void frmTableDataTransfer_Load(object sender, EventArgs e)
        {
            txtDisplay.Clear();

            cmbServer.Items.Add("SELADEV10");
            cmbServer.Items.Add("SELADEV11");
            cmbServer.Items.Add("SELCDEV10");
        }

        private void btnScript_Click(object sender, EventArgs e)
        {
            cmbServer.BackColor = Color.White;
            cmbDatabase.BackColor = Color.White;
            cmbTable.BackColor = Color.White;
            txtSource.BackColor = Color.White;

            if (cmbServer.SelectedIndex == -1)
            {
                cmbServer.BackColor = Color.Red;
                return;
            }

            if (cmbDatabase.SelectedIndex == -1)
            {
                cmbDatabase.BackColor = Color.Red;
                return;
            }

            if (cmbTable.SelectedIndex == -1)
            {
                cmbTable.BackColor = Color.Red;
                return;
            }

            if (txtSource.Text.Length == 0)
            {
                txtSource.BackColor = Color.Red;
                return;
            }

            txtDisplay.Clear();
            ResultOutput(GenerateScript(cmbServer.Items[cmbServer.SelectedIndex].ToString(), txtUser.Text,
                txtPass.Text, cmbDatabase.Items[cmbDatabase.SelectedIndex].ToString(), cmbTable.Items[cmbTable.SelectedIndex].ToString(),
                txtSource.Text));

        }

        private void cmbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbDatabase.Items.Clear();
            cmbTable.Items.Clear();
            if (cmbServer.SelectedIndex != -1)
                PopulateDatabases(cmbServer.Items[cmbServer.SelectedIndex].ToString(), txtUser.Text, txtPass.Text,
                    "Master");

            if (cmbDatabase.Items.Count > 0)
                cmbDatabase.SelectedIndex = 0;
        }

        private void chkWindowsAuth_CheckedChanged(object sender, EventArgs e)
        {
            bool _Enabled = !chkWindowsAuth.Checked;

            txtUser.Enabled = _Enabled;
            txtPass.Enabled = _Enabled;
            lblUser.Enabled = _Enabled;
            lblPass.Enabled = _Enabled;
        }

        private void cmbDatabase_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbTable.Items.Clear();
            if (cmbServer.SelectedIndex != -1)
                PopulateTables(cmbServer.Items[cmbServer.SelectedIndex].ToString(), txtUser.Text, txtPass.Text,
                    cmbDatabase.Items[cmbDatabase.SelectedIndex].ToString());

            if (cmbTable.Items.Count > 0)
                cmbTable.SelectedIndex = 0;
        }

        private void PopulateDatabases(string s, string u, string p, string d)
        {
            string q = "SELECT name FROM sys.sysdatabases WHERE HAS_DBACCESS(name) = 1 AND LEFT(name, 3) = 'RDB' ORDER BY Name ASC";
            using (SqlConnection sc = new SqlConnection(Database.BuildConnectionString(s, u, p, d, chkWindowsAuth.Checked)))
            {
                sc.Open();
                using (SqlCommand cmd = new SqlCommand(q, sc))
                {
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cmbDatabase.Items.Add(dr.GetString(0));
                        }
                        dr.Dispose();
                    }
                    cmd.Dispose();
                }
                sc.Close();
                sc.Dispose();
            }
        }

        private void PopulateTables(string s, string u, string p, string d)
        {
            string q = "SELECT Name FROM sys.tables WHERE type_desc = 'USER_TABLE' ORDER BY name ASC";
            using (SqlConnection sc = new SqlConnection(Database.BuildConnectionString(s, u, p, d, chkWindowsAuth.Checked)))
            {
                sc.Open();
                using (SqlCommand cmd = new SqlCommand(q, sc))
                {
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cmbTable.Items.Add(dr.GetString(0));
                        }
                        dr.Dispose();
                    }
                    cmd.Dispose();
                }
                sc.Close();
                sc.Dispose();
            }
        }

        private string GenerateScript(string destS, string destU, string destP, string destD, string destT, string srcS)
        {
            string ssql = "";
            string sInsertScript = "";
            string sSelectScript = "";
            string sSelectCondition = "";
            string sUpdateScript = "";
            string sUpdateCondition = "";

            int iColumnCount = ColumnCount(destS, destU, destP, destD, destT);

            ssql =
                "SELECT  C.COLUMN_NAME, C.TABLE_SCHEMA, [IsPK] = CASE WHEN K.COLUMN_NAME IS NULL THEN 0 ELSE 1 END FROM INFORMATION_SCHEMA.COLUMNS C " +
                "LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE K ON C.TABLE_CATALOG = K.CONSTRAINT_CATALOG AND C.TABLE_SCHEMA = k.TABLE_SCHEMA " +
                "AND C.TABLE_NAME = K.TABLE_NAME AND C.ORDINAL_POSITION = K.ORDINAL_POSITION WHERE C.TABLE_NAME = '"+ destT+"' " +
                "AND C.TABLE_CATALOG = '"+destD+"' ORDER BY IsPk DESC, C.ORDINAL_POSITION ASC ";

            sInsertScript += string.Format("INSERT INTO {0} (", destT);
            sUpdateScript += string.Format("UPDATE dest\nSET ");
            sSelectScript += "SELECT ";

            int RowCount = 1;
            using (SqlConnection sc = new SqlConnection(Database.BuildConnectionString(destS, destU, destP, destD, chkWindowsAuth.Checked)))
            {
                sc.Open();
                using (SqlCommand cmd = new SqlCommand(ssql, sc))
                {
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (dr.GetInt32(2) == 1)
                            {
                                if (sSelectCondition.Length == 0)
                                {
                                    sSelectCondition +=
                                        string.Format("FROM {1}.{2}.{3}.{4} WHERE {0} NOT IN (SELECT {0} FROM {4});",
                                            dr.GetString(0), srcS, destD, dr.GetString(1), destT);
                                    sUpdateCondition +=
                                        string.Format("FROM {4} dest JOIN {1}.{2}.{3}.{4} src ON dest.{0} = src.{0};",
                                            dr.GetString(0), srcS, destD, dr.GetString(1), destT);
                                }
                            }
                            else
                            {
                                sInsertScript += string.Format("{0}", dr.GetString(0));
                                sSelectScript += string.Format("{0}", dr.GetString(0));
                                sUpdateScript += string.Format("dest.{0} = src.{0}\n", dr.GetString(0));
                                
                                RowCount++;
                                if (RowCount != iColumnCount)
                                {
                                    sInsertScript += ", ";
                                    sSelectScript += ", ";
                                    sUpdateScript += ", ";
                                }
                            }
                        }
                        dr.Dispose();
                    }
                    cmd.Dispose();
                }
                sc.Close();
                sc.Dispose();
            }

            sInsertScript += ") ";

            return string.Format("USE {5};" +
                                 "\n" +
                                 "\n" +
                                 "--Insert what isnt already there" +
                                 "\n{0}" +
                                 "\n{1}" +
                                 "\n{2}" +
                                 "\n" +
                                 "\n" +
                                 "--Update everything with the source details" +
                                 "\n{3}" +
                                 "{4}", sInsertScript, sSelectScript, sSelectCondition, sUpdateScript, sUpdateCondition, destD);
        }

        private int ColumnCount(string destS, string destU, string destP, string destD, string destT)
        {
            string ssql =
                "SELECT  COUNT(C.COLUMN_NAME) FROM INFORMATION_SCHEMA.COLUMNS C " +
                "LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE K ON C.TABLE_CATALOG = K.CONSTRAINT_CATALOG AND C.TABLE_SCHEMA = k.TABLE_SCHEMA " +
                "AND C.TABLE_NAME = K.TABLE_NAME AND C.ORDINAL_POSITION = K.ORDINAL_POSITION WHERE C.TABLE_NAME = '" + destT + "' " +
                "AND C.TABLE_CATALOG = '" + destD + "' ";

            int iCount = 0;

            using (SqlConnection sc = new SqlConnection(Database.BuildConnectionString(destS, destU, destP, destD, chkWindowsAuth.Checked)))
            {
                sc.Open();
                using (SqlCommand cmd = new SqlCommand(ssql, sc))
                {
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            iCount = dr.GetInt32(0);
                        }
                        dr.Dispose();
                    }
                    cmd.Dispose();
                }
                sc.Close();
                sc.Dispose();
            }
            return iCount;
        }

        public void ResultOutput(string m)
        {
            TextWriter consoleDirect = new TextBoxStreamWriter(this.txtDisplay);
            Console.SetOut(consoleDirect);
            consoleDirect.WriteLine(m);
        }

    }

    public class TextBoxStreamWriter : TextWriter
    {
        TextBox _output = null;

        public TextBoxStreamWriter(TextBox output)
        {
            _output = output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            _output.AppendText(value.ToString()); // When character data is written, append it to the text box.
        }

        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
        }
    }
}
