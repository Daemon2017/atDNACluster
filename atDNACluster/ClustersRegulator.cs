﻿using System;
using System.Windows.Forms;

namespace atDNACluster
{
    public partial class ClustersRegulator : Form
    {
        public ClustersRegulator()
        {
            InitializeComponent();
        }

        public int numberOfClusters = 2;

        private void button1_Click(object sender, EventArgs e)
        {
            numberOfClusters = (int)numericUpDown1.Value;

            Close();
        }
    }
}
