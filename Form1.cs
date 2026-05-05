using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace bd
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=13524;Database=cd_shop";

        public Form1()
        {
            InitializeComponent();

            btnExecute.Click += btnExecute_Click;

            LoadTables();
        }

        private void LoadTables()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    string getTablesQuery = @"
                        SELECT table_name 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_type = 'BASE TABLE'";

                    using (var cmd = new NpgsqlCommand(getTablesQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tableName = reader.GetString(0);
                            CreateTabForTable(tableName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке таблиц: {ex.Message}");
            }
        }

        private void CreateTabForTable(string tableName)
        {
            TabPage tabPage = new TabPage(tableName);

            DataGridView dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true
            };

            DataTable dataTable = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();
                    string query = $"SELECT * FROM \"{tableName}\"";
                    using (var adapter = new NpgsqlDataAdapter(query, conn))
                    {
                        adapter.Fill(dataTable);
                    }
                }

                dgv.DataSource = dataTable;
                tabPage.Controls.Add(dgv);
                tabControlMain.TabPages.Add(tabPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных из таблицы {tableName}: {ex.Message}");
            }
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            string query = txtQuery.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Введите SQL запрос!");
                return;
            }

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    conn.Open();

                    if (query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    {
                        DataTable dt = new DataTable();
                        using (var adapter = new NpgsqlDataAdapter(query, conn))
                        {
                            adapter.Fill(dt);
                        }
                        dgvQueryResults.DataSource = dt;
                    }
                    else
                    {
                        using (var cmd = new NpgsqlCommand(query, conn))
                        {
                            int rowsAffected = cmd.ExecuteNonQuery();
                            MessageBox.Show($"Запрос выполнен успешно. Затронуто строк: {rowsAffected}");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выполнения запроса: {ex.Message}", "Ошибка SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
