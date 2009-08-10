﻿namespace AsioTestGUI
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナ変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            FinalizeAll();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナで生成されたコード

        /// <summary>
        /// デザイナ サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディタで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.listBoxDrivers = new System.Windows.Forms.ListBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.buttonLoadDriver = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.checkedListBoxInput = new System.Windows.Forms.CheckedListBox();
            this.checkedListBoxOutput = new System.Windows.Forms.CheckedListBox();
            this.buttonStart = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBoxDrivers
            // 
            this.listBoxDrivers.FormattingEnabled = true;
            this.listBoxDrivers.ItemHeight = 15;
            this.listBoxDrivers.Location = new System.Drawing.Point(6, 21);
            this.listBoxDrivers.Name = "listBoxDrivers";
            this.listBoxDrivers.Size = new System.Drawing.Size(413, 109);
            this.listBoxDrivers.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.buttonLoadDriver);
            this.groupBox1.Controls.Add(this.listBoxDrivers);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(425, 176);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "1. Select ASIO driver";
            // 
            // buttonLoadDriver
            // 
            this.buttonLoadDriver.Enabled = false;
            this.buttonLoadDriver.Location = new System.Drawing.Point(6, 136);
            this.buttonLoadDriver.Name = "buttonLoadDriver";
            this.buttonLoadDriver.Size = new System.Drawing.Size(184, 33);
            this.buttonLoadDriver.TabIndex = 1;
            this.buttonLoadDriver.Text = "Load Driver";
            this.buttonLoadDriver.UseVisualStyleBackColor = true;
            this.buttonLoadDriver.Click += new System.EventHandler(this.buttonLoadDriver_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.buttonStart);
            this.groupBox2.Controls.Add(this.checkedListBoxOutput);
            this.groupBox2.Controls.Add(this.checkedListBoxInput);
            this.groupBox2.Location = new System.Drawing.Point(12, 194);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(425, 435);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "2. Select IO channels to use";
            // 
            // checkedListBoxInput
            // 
            this.checkedListBoxInput.FormattingEnabled = true;
            this.checkedListBoxInput.Location = new System.Drawing.Point(6, 21);
            this.checkedListBoxInput.Name = "checkedListBoxInput";
            this.checkedListBoxInput.Size = new System.Drawing.Size(413, 191);
            this.checkedListBoxInput.TabIndex = 0;
            // 
            // checkedListBoxOutput
            // 
            this.checkedListBoxOutput.FormattingEnabled = true;
            this.checkedListBoxOutput.Location = new System.Drawing.Point(6, 218);
            this.checkedListBoxOutput.Name = "checkedListBoxOutput";
            this.checkedListBoxOutput.Size = new System.Drawing.Size(413, 174);
            this.checkedListBoxOutput.TabIndex = 1;
            // 
            // buttonStart
            // 
            this.buttonStart.Enabled = false;
            this.buttonStart.Location = new System.Drawing.Point(6, 398);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(184, 30);
            this.buttonStart.TabIndex = 2;
            this.buttonStart.Text = "Start";
            this.buttonStart.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(614, 651);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxDrivers;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button buttonLoadDriver;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.CheckedListBox checkedListBoxOutput;
        private System.Windows.Forms.CheckedListBox checkedListBoxInput;
    }
}

