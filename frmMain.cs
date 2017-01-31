using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualBasic;

namespace ClarifiLogBuilder
{
    public partial class frmMain : Form
    {
        public class ObjectDetails
        {
            public string ObjectType;
            public string ObjectName;
            public string Installation;
        }

        #region SqlFunctions
        public struct SqlFunction
        {
            public int ID;
            public string FormName;
            public string FunctionName;

            public SqlFunction(int _id, string _formName, string _functionName)
            {
                ID = _id;
                FormName = _formName;
                FunctionName = _functionName;
            }
        }
        public List<SqlFunction> SqlFunctions = new List<SqlFunction>();

        private void PopulateSqlFunctions(ref List<SqlFunction> sF)
        {
            int i = 0;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type t in assembly.GetTypes().Where(x => x.BaseType == typeof(Form) && x.Namespace.StartsWith("Clarifi") 
                    && x.Namespace != Assembly.GetExecutingAssembly().EntryPoint.DeclaringType.Namespace ))
                {
                    sF.Add(new SqlFunction(i,
                        t.UnderlyingSystemType.FullName, t.UnderlyingSystemType.FullName.Substring(t.Namespace.Length + 4)));
                    i++;
                }
            }

            CreateMenuLinks(ref sF);
        }

        private void CreateMenuLinks(ref List<SqlFunction> sF)
        {
            if (sF.Count > 0)
            {
                string mnuRootName = "SqlControls";
                ToolStripMenuItem mnuSqlFunctions = new ToolStripMenuItem(mnuRootName, null, delegate(object sender, EventArgs args) { OpenFunction(-1); });
                msMenu.Items.Add(mnuSqlFunctions);

                foreach (ToolStripMenuItem toolStripMenuItem in msMenu.Items)
                {
                    CreateSubMenu(toolStripMenuItem, mnuRootName, toolStripMenuItem.Text);
                }
            }
        }

        private void CreateSubMenu(ToolStripMenuItem mnuItems, string sRootName, string sName)
        {
            if (sName == sRootName)
            {
                foreach (SqlFunction sqlFunction in SqlFunctions)
                {
                    mnuItems.DropDownItems.Add(sqlFunction.FunctionName, null,
                        delegate(object sender, EventArgs args) { OpenFunction(sqlFunction.ID); });
                }
            }
        }

        private void OpenFunction(int ID)
        {
            if (ID < 0)
                return;

            string formFunctName = SqlFunctions.Find(x => x.ID == ID).FunctionName;
            string formName = SqlFunctions.Find(x => x.ID == ID).FormName;
            
            try
            {
                Type t = Type.GetType(formName);
                Form frm = (Form) Activator.CreateInstance(t);
                frm.ShowDialog();
            }
            catch (Exception ex)
            {
                txtOutput.AppendText("\nError opening form "+ formFunctName +": \n\t" + ex.Message + "\n");
            }

        }
        
        #endregion

        public frmMain()
        {
            InitializeComponent();
            PopulateSqlFunctions(ref SqlFunctions);
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            txtUser.Text = "sysdevteam";
            txtPass.Text = "sys516";
            checkBox1.Checked = true;
            checkBox1_CheckedChanged(sender, e);
            btnFindMessageStructure.Enabled = false;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GetDatabases();
        }

        private void GetDatabases()
        {
            lstDatabases.Items.Clear();
            if (cmbServer.SelectedIndex != -1)
                PopulateDatabases(cmbServer.Items[cmbServer.SelectedIndex].ToString(), txtUser.Text, txtPass.Text,
                    "Master");

            if (lstDatabases.Items.Count > 0)
                lstDatabases.SelectedIndex = 0;
        }

        private void PopulateDatabases(string s, string u, string p, string d)
        {
            string q = "SELECT name FROM sys.sysdatabases WHERE HAS_DBACCESS(name) = 1 AND LEFT(name, 3) = 'MDB'";
            using (SqlConnection sc = new SqlConnection(Database.BuildConnectionString(s, u, p, d, checkBox1.Checked)))
            {
                sc.Open();
                using (SqlCommand cmd = new SqlCommand(q, sc))
                {
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lstDatabases.Items.Add(dr.GetString(0));
                        }
                        dr.Dispose();
                    }
                    cmd.Dispose();
                }
                sc.Close();
                sc.Dispose();
            }
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            if (cmbServer.SelectedIndex == -1 || lstDatabases.SelectedIndex == -1)
                MessageBox.Show("Select Server & Database");
            else
            {
                try
                {
                    string path = string.Empty, type = string.Empty, objectType = string.Empty;
                    ObjectDetails od = new ObjectDetails();
                    string DB = lstDatabases.Items[lstDatabases.SelectedIndex].ToString().Substring(4);
                    od.Installation = DB;
                    Database db =
                        new Database(Database.BuildConnectionString(cmbServer.Items[cmbServer.SelectedIndex].ToString(),
                            txtUser.Text,
                            txtPass.Text, lstDatabases.Items[lstDatabases.SelectedIndex].ToString(), checkBox1.Checked));
                    db.GetWFO(txtObject.Text, "", ref type, ref path, ref objectType, ref od);
                    if (path != string.Empty)
                        ResultOutput(
                            string.Format(
                                "ID:\n\t{0}\nPath:\n\t{1}\nInstallation:\n\t{2}\nObjectName\n\t{3}\nObjectType\n\t{4}\n\n",
                                txtObject.Text, path, od.Installation, od.ObjectName, od.ObjectType));
                    else
                        ResultOutput(string.Format("ID:\n\t{0} Not Found\n\n", txtObject.Text));

                    if (chkInsert.Checked)
                    {
                        string ObjectName = objectType.Substring(0, 1) + txtObject.Text;
                        string User = Environment.UserName;

                        DB = DB.Substring(4, DB.Length - 4);
                        string InsertQuery =
                            "\nUSE RDB_SEL020004D; DECLARE @LogDetails VARCHAR(1000); DECLARE @LogReason VARCHAR(1000);";
                        InsertQuery += string.Format("\nSET @LogDetails = '';");
                        InsertQuery += string.Format("\nSET @LogReason = '';");
                        InsertQuery +=
                            string.Format(
                                "\nINSERT INTO tbl_0000000022 (Installation, Path, ObjectName, ObjectType, ChangeMade, ChangeReason, DateChanged, Responsibility)");
                        InsertQuery +=
                            string.Format(
                                "\nVALUES('{0}', '{1}', '{2}', '{3}', @LogDetails, @LogReason, GETDATE(), '{4}');", DB,
                                path, ObjectName, objectType, User);

                        ResultOutput(InsertQuery);
                    }

                    txtObject.Text = string.Empty;
                }
                catch (SqlException ex)
                {
                    MessageBox.Show(ex.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

        }

        private void KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnFind.PerformClick();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            txtOutput.Clear();
        }

        public void ResultOutput(string m)
        {
            TextWriter consoleDirect = new TextBoxStreamWriter(this.txtOutput);
            Console.SetOut(consoleDirect);
            consoleDirect.WriteLine(m);
        }

        private void cmbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            GetDatabases();
        }

        private void btnFindMessageStructure_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbServer.SelectedIndex == -1 || lstDatabases.SelectedIndex == -1)
                    MessageBox.Show("Select Server & Database");
                else
                {

                    string _MessageName = Interaction.InputBox("Input Message Name", "Message", "", -1, -1);
                    if (_MessageName == string.Empty)
                        ResultOutput("Empty string");
                    else
                    {
                        {
                            Database db =
                                new Database(
                                    Database.BuildConnectionString(cmbServer.Items[cmbServer.SelectedIndex].ToString(),
                                        txtUser.Text,
                                        txtPass.Text, lstDatabases.Items[lstDatabases.SelectedIndex].ToString(),
                                        checkBox1.Checked));
                            ResultOutput(db.GetMessageStructure(_MessageName));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }




        }

        private void lstDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnFindMessageStructure.Enabled = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            txtPass.Enabled = !checkBox1.Checked;
            txtUser.Enabled = !checkBox1.Checked;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(Environment.ExitCode);
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
