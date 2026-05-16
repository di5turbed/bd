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

            Panel pnlOrdersTop = new Panel { Dock = DockStyle.Top, Height = 40 };
            Button btnAddOrder = new Button
            {
                Text = "➕ Создать заказ",
                Left = 10,
                Top = 8,
                Width = 140,
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat
            };
            btnAddOrder.FlatAppearance.BorderSize = 0;
            btnAddOrder.Click += async (s, e) => await ShowCreateOrderDialogAsync();

            pnlOrdersTop.Controls.Add(btnAddOrder);

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
            gbOrders.Controls.Add(pnlOrdersTop);
            dgvOrders.BringToFront();

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
            tabControlMain.TabPages.Add(ordersTab);

            dgvOrders.SelectionChanged += async (s, e) => await dgvOrders_SelectionChangedAsync();
        }

        private async Task ShowCreateOrderDialogAsync()
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Новый заказ";
                dialog.Size = new Size(360, 260);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                Label lblCustomer = new Label { Text = "Клиент:", Left = 20, Top = 25, Width = 100 };
                ComboBox cmbCustomer = new ComboBox { Left = 120, Top = 20, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

                Button btnAddCustomer = new Button { Text = "➕", Left = 275, Top = 19, Width = 30, Height = 23, FlatStyle = FlatStyle.Flat };

                Func<Task> loadCustomersAsync = async () =>
                {
                    try
                    {
                        using (var conn = new NpgsqlConnection(connectionString))
                        {
                            await conn.OpenAsync();
                            string query = "SELECT id, fio FROM customer ORDER BY fio";
                            using (var cmd = new NpgsqlCommand(query, conn))
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                DataTable dtCustomers = new DataTable();
                                dtCustomers.Load(reader);
                                cmbCustomer.DataSource = dtCustomers;
                                cmbCustomer.DisplayMember = "fio";
                                cmbCustomer.ValueMember = "id";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при загрузке клиентов: " + ex.Message);
                    }
                };

                await loadCustomersAsync();

                btnAddCustomer.Click += async (s, ev) =>
                {
                    using (Form custDialog = new Form())
                    {
                        custDialog.Text = "Новый клиент";
                        custDialog.Size = new Size(320, 200);
                        custDialog.StartPosition = FormStartPosition.CenterParent;
                        custDialog.FormBorderStyle = FormBorderStyle.FixedDialog;

                        Label lblFio = new Label { Text = "ФИО:", Left = 20, Top = 25, Width = 60 };
                        TextBox txtFio = new TextBox { Left = 90, Top = 22, Width = 180 };

                        Label lblAddress = new Label { Text = "Адрес:", Left = 20, Top = 65, Width = 60 };
                        TextBox txtAddress = new TextBox { Left = 90, Top = 62, Width = 180 };

                        Button btnCustSave = new Button { Text = "Сохранить", Left = 110, Top = 110, Width = 80, DialogResult = DialogResult.OK };
                        Button btnCustCancel = new Button { Text = "Отмена", Left = 200, Top = 110, Width = 80, DialogResult = DialogResult.Cancel };

                        custDialog.Controls.AddRange(new Control[] { lblFio, txtFio, lblAddress, txtAddress, btnCustSave, btnCustCancel });
                        custDialog.AcceptButton = btnCustSave;
                        custDialog.CancelButton = btnCustCancel;

                        if (custDialog.ShowDialog() == DialogResult.OK)
                        {
                            string newFio = txtFio.Text.Trim();
                            string newAddress = txtAddress.Text.Trim();

                            if (string.IsNullOrEmpty(newFio))
                            {
                                MessageBox.Show("ФИО не может быть пустым!");
                                return;
                            }

                            try
                            {
                                int insertedId = 0;
                                using (var conn = new NpgsqlConnection(connectionString))
                                {
                                    await conn.OpenAsync();
                                    string insertCustQuery = @"
                                        INSERT INTO ""customer"" (fio, address) 
                                        VALUES (@fio, @address) 
                                        RETURNING id";

                                    using (var cmd = new NpgsqlCommand(insertCustQuery, conn))
                                    {
                                        cmd.Parameters.AddWithValue("@fio", newFio);
                                        cmd.Parameters.AddWithValue("@address", newAddress);
                                        insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                    }
                                }

                                await loadCustomersAsync();
                                cmbCustomer.SelectedValue = insertedId;
                                statusLabel.Text = $"Клиент '{newFio}' добавлен.";
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Ошибка добавления клиента: {ex.Message}");
                            }
                        }
                    }
                };

                Label lblOrderNum = new Label { Text = "Номер заказа:", Left = 20, Top = 65, Width = 100 };
                NumericUpDown nudOrderNum = new NumericUpDown { Left = 120, Top = 60, Width = 185, Maximum = 999999999, Minimum = 1 };

                Label lblDate = new Label { Text = "Дата заказа:", Left = 20, Top = 105, Width = 100 };
                DateTimePicker dtpDate = new DateTimePicker { Left = 120, Top = 100, Width = 185, Format = DateTimePickerFormat.Short };

                Button btnSave = new Button { Text = "Сохранить", Left = 120, Top = 160, Width = 85, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Отмена", Left = 220, Top = 160, Width = 85, DialogResult = DialogResult.Cancel };

                dialog.Controls.AddRange(new Control[] { lblCustomer, cmbCustomer, btnAddCustomer, lblOrderNum, nudOrderNum, lblDate, dtpDate, btnSave, btnCancel });
                dialog.AcceptButton = btnSave;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (cmbCustomer.SelectedValue == null)
                    {
                        MessageBox.Show("Выберите клиента!");
                        return;
                    }

                    try
                    {
                        using (var conn = new NpgsqlConnection(connectionString))
                        {
                            await conn.OpenAsync();
                            string insertQuery = @"
                                INSERT INTO ""order"" (customer_id, order_number, order_date) 
                                VALUES (@cid, @onum, @odate)";

                            using (var cmd = new NpgsqlCommand(insertQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@cid", Convert.ToInt32(cmbCustomer.SelectedValue));
                                cmd.Parameters.AddWithValue("@onum", (int)nudOrderNum.Value);
                                cmd.Parameters.AddWithValue("@odate", dtpDate.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        statusLabel.Text = "Новый заказ создан!";
                        await RefreshOrdersGridAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении заказа: {ex.Message}");
                    }
                }
            }
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
                        ORDER BY o.id DESC";

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

        // Модифицированный метод: создает редактируемые таблицы с кнопкой сохранения
        private async Task CreateTabForTableAsync(string tableName)
        {
            TabPage tabPage = new TabPage(tableName);

            // Нижняя панель для кнопки сохранения
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 45 };
            Button btnSaveTable = new Button
            {
                Text = "💾 Сохранить изменения",
                Left = 10,
                Top = 8,
                Width = 180,
                BackColor = Color.LightSkyBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveTable.FlatAppearance.BorderSize = 0;
            pnlBottom.Controls.Add(btnSaveTable);

            DataGridView dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,     // Разрешаем вставлять строки прямо в сетку
                AllowUserToDeleteRows = true,  // Разрешаем удалять строки кнопкой Delete
                ReadOnly = false,              // РЕЖИМ РЕДАКТИРОВАНИЯ ЯЧЕЕК ВКЛЮЧЕН!
                BackgroundColor = Color.WhiteSmoke
            };

            DataTable dataTable = new DataTable();
            // Используем DataAdapter для связывания табличных данных с БД
            NpgsqlDataAdapter adapter = new NpgsqlDataAdapter($"SELECT * FROM \"{tableName}\"", connectionString);

            try
            {
                // Загружаем данные асинхронно
                await Task.Run(() => adapter.Fill(dataTable));
                dgv.DataSource = dataTable;

                // Вешаем логику сохранения на кнопку
                btnSaveTable.Click += async (s, e) =>
                {
                    try
                    {
                        dgv.EndEdit(); // Фиксируем текущую редактируемую ячейку (если пользователь забыл выйти из нее)
                        statusLabel.Text = $"Сохранение изменений в {tableName}...";

                        await Task.Run(() =>
                        {
                            // CommandBuilder автоматически создаёт команды UPDATE/INSERT/DELETE на основе структуры первичного ключа
                            using (var builder = new NpgsqlCommandBuilder(adapter))
                            {
                                adapter.Update(dataTable);
                            }
                        });

                        statusLabel.Text = $"Изменения в '{tableName}' успешно сохранены.";
                        MessageBox.Show("Все изменения успешно синхронизированы с БД!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        statusLabel.Text = "Ошибка сохранения.";
                        MessageBox.Show($"Не удалось сохранить изменения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                tabPage.Controls.Add(dgv);
                tabPage.Controls.Add(pnlBottom);
                dgv.BringToFront(); // Размещаем таблицу над нижней панелью

                tabControlMain.TabPages.Add(tabPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания вкладки {tableName}: {ex.Message}");
            }
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