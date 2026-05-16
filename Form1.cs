using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace bd
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=13524;Database=cd_shop";

        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        private DataGridView dgvOrders;
        private DataGridView dgvOrderDetails;

        public Form1()
        {
            InitializeComponent();
            InitializeAdvancedUI();
            CreateOrdersTab();

            btnExecute.Click -= btnExecute_Click;
            btnExecute.Click += async (s, e) => await ExecuteQueryAsync();

            this.Load += async (s, e) => await LoadTablesAsync();
        }

        private void InitializeAdvancedUI()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Text = "Ожидание..." };
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);
        }

        private void CreateOrdersTab()
        {
            TabPage ordersTab = new TabPage("Заказы");

            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 200
            };

            GroupBox gbOrders = new GroupBox { Text = "Заказы (таблица order)", Dock = DockStyle.Fill };
            dgvOrders = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            gbOrders.Controls.Add(dgvOrders);
            splitContainer.Panel1.Controls.Add(gbOrders);

            GroupBox gbDetails = new GroupBox { Text = "Позиции заказа (таблица order_position)", Dock = DockStyle.Fill };
            dgvOrderDetails = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.WhiteSmoke
            };
            gbDetails.Controls.Add(dgvOrderDetails);
            splitContainer.Panel2.Controls.Add(gbDetails);

            ordersTab.Controls.Add(splitContainer);

            // Надежное добавление вкладки в конец списка
            tabControlMain.TabPages.Add(ordersTab);

            dgvOrders.SelectionChanged += async (s, e) => await dgvOrders_SelectionChangedAsync();
        }

        private async Task LoadTablesAsync()
        {
            statusLabel.Text = "Загрузка данных...";
            try
            {
                await RefreshOrdersGridAsync();

                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string getTablesQuery = @"
                        SELECT table_name 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_type = 'BASE TABLE'
                        ORDER BY table_name";

                    using (var cmd = new NpgsqlCommand(getTablesQuery, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string tableName = reader.GetString(0);

                            // Скрываем таблицы order и order_position, так как они на отдельной удобной вкладке
                            if (tableName.Equals("order", StringComparison.OrdinalIgnoreCase) ||
                                tableName.Equals("order_position", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            await CreateTabForTableAsync(tableName);
                        }
                    }
                }
                statusLabel.Text = "Готово.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}");
                statusLabel.Text = "Ошибка.";
            }
        }

        private async Task RefreshOrdersGridAsync()
        {
            try
            {
                DataTable dtOrders = new DataTable();
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query = @"
                        SELECT o.id AS ""ID"",
                               o.order_number AS ""Номер заказа"",
                               c.fio AS ""Клиент (ФИО)"",
                               o.order_date AS ""Дата заказа""
                        FROM ""order"" o
                        LEFT JOIN ""customer"" c ON o.customer_id = c.id
                        ORDER BY o.order_date DESC";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dtOrders.Load(reader);
                    }
                }
                dgvOrders.DataSource = dtOrders;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка загрузки списка заказов.";
                Debug.WriteLine($"Дебаг: {ex.Message}");
            }
        }

        private async Task dgvOrders_SelectionChangedAsync()
        {
            if (dgvOrders.SelectedRows.Count == 0)
            {
                dgvOrderDetails.DataSource = null;
                return;
            }

            try
            {
                var selectedRow = dgvOrders.SelectedRows[0];
                object orderIdValue = selectedRow.Cells["ID"].Value;

                if (orderIdValue == null || orderIdValue == DBNull.Value) return;

                DataTable dtDetails = new DataTable();
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT op.id AS ""ID Позиции"", 
                               d.title AS ""Название диска"", 
                               op.quantity AS ""Количество"", 
                               d.price AS ""Цена (шт.)"",
                               (op.quantity * d.price) AS ""Сумма""
                        FROM ""order_position"" op
                        LEFT JOIN ""disk"" d ON op.disk_id = d.id
                        WHERE op.order_id = @OrderId";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", Convert.ToInt32(orderIdValue));
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dtDetails.Load(reader);
                        }
                    }
                }

                dgvOrderDetails.DataSource = dtDetails;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Не удалось загрузить позиции заказа.";
                dgvOrderDetails.DataSource = null;
            }
        }

        private async Task CreateTabForTableAsync(string tableName)
        {
            TabPage tabPage = new TabPage(tableName);
            DataGridView dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.WhiteSmoke
            };

            DataTable dataTable = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query = $"SELECT * FROM \"{tableName}\"";
                    using (var cmd = new NpgsqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }

                dgv.DataSource = dataTable;
                tabPage.Controls.Add(dgv);

                tabControlMain.TabPages.Add(tabPage);
            }
            catch { }
        }

        private async Task ExecuteQueryAsync()
        {
            string query = txtQuery.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Введите SQL запрос!");
                return;
            }

            btnExecute.Enabled = false;
            statusLabel.Text = "Выполнение...";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    if (query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                        query.IndexOf("RETURNING", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        DataTable dt = new DataTable();
                        using (var cmd = new NpgsqlCommand(query, conn))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                        dgvQueryResults.DataSource = dt;
                        statusLabel.Text = $"Успешно. Строк: {dt.Rows.Count}.";
                    }
                    else
                    {
                        using (var cmd = new NpgsqlCommand(query, conn))
                        {
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            statusLabel.Text = $"Успешно. Затронуто строк: {rowsAffected}.";
                        }
                        await RefreshOrdersGridAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка выполнения.";
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                btnExecute.Enabled = true;
            }
        }

        private void btnExecute_Click(object sender, EventArgs e) { }
    }
}