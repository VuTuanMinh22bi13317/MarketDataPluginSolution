namespace MddsSocketTest
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtSeq = new System.Windows.Forms.TextBox();
            this.btnStartSending = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnStartReceiving = new System.Windows.Forms.Button();
            this.txtReceived = new System.Windows.Forms.TextBox(); // Thêm TextBox để hiển thị dữ liệu nhận
            this.SuspendLayout();
            // 
            // txtSeq
            // 
            this.txtSeq.Location = new System.Drawing.Point(12, 12);
            this.txtSeq.Name = "txtSeq";
            this.txtSeq.Size = new System.Drawing.Size(100, 23);
            this.txtSeq.TabIndex = 0;
            // 
            // btnStartSending
            // 
            this.btnStartSending.Location = new System.Drawing.Point(12, 41);
            this.btnStartSending.Name = "btnStartSending";
            this.btnStartSending.Size = new System.Drawing.Size(100, 23);
            this.btnStartSending.TabIndex = 1;
            this.btnStartSending.Text = "Start";
            this.btnStartSending.UseVisualStyleBackColor = true;
            this.btnStartSending.Click += new System.EventHandler(this.btnStartSending_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(118, 41);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 23);
            this.btnStop.TabIndex = 2;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnStartReceiving
            // 
            this.btnStartReceiving.Location = new System.Drawing.Point(224, 41); // Vị trí bên phải btnStop
            this.btnStartReceiving.Name = "btnStartReceiving";
            this.btnStartReceiving.Size = new System.Drawing.Size(100, 23);
            this.btnStartReceiving.TabIndex = 3;
            this.btnStartReceiving.Text = "Start Receiving";
            this.btnStartReceiving.UseVisualStyleBackColor = true;
            this.btnStartReceiving.Click += new System.EventHandler(this.btnStartReceiving_Click);
            // 
            // txtReceived
            // 
            this.txtReceived.Location = new System.Drawing.Point(12, 70); // Dưới các nút
            this.txtReceived.Multiline = true;
            this.txtReceived.ScrollBars = ScrollBars.Vertical;
            this.txtReceived.Size = new System.Drawing.Size(776, 368); // Chiếm phần lớn form
            this.txtReceived.Name = "txtReceived";
            this.txtReceived.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.txtReceived);
            this.Controls.Add(this.btnStartReceiving);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStartSending);
            this.Controls.Add(this.txtSeq);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox txtSeq;
        private System.Windows.Forms.Button btnStartSending;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnStartReceiving;
        private System.Windows.Forms.TextBox txtReceived; // Khai báo TextBox để hiển thị dữ liệu nhận
    }
}