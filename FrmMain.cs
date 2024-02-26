using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows.Forms;



namespace NumberProcessor_Global_2022
{
    public partial class FrmMain : Form
    {
        private string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\NumberProcessor_Global_2022";
        private bool start = false;
        private int NUM_OF_THREADS = 30;//1,30

        #region Start Button
        private int NUM_OF_GRPROWCOL = 10;
        private void btnStartBatchFilter_Click(object sender, EventArgs e)
        {
            if (!start)
            {
                int targetNumCols = (int)numTargetCols.Value;
                string inputFilePath = txtBrowseInput.Text;
                bool isBatchScanFilter = chkBatchScanFilter.Checked;
                bool isSummationFilter = chkSummationFilter.Checked;
                string batchRowsText = txtBatchRows.Text;
                bool[] batchCols = { chkBatchA.Checked, chkBatchB.Checked, chkBatchC.Checked, chkBatchD.Checked, chkBatchE.Checked, chkBatchF.Checked };
                bool[] sumColumns = { chkBatchColA.Checked, chkBatchColB.Checked, chkBatchColC.Checked, chkBatchColD.Checked, chkBatchColE.Checked, chkBatchColF.Checked };
                int batchMinMatch = (int)numBatchMinMatch.Value;
                int numMinMatch = (int)this.numMinMatch.Value;
                bool batchRetainMatch = rdoBatchMatch.Checked;
                bool sumRetainMatched = chkMatchedSum.Checked;
                bool absolute = chkAbsolute.Checked;


                bool isGroupMemberFilter = chkGroupMemberFilter.Checked;
                List<int[]> groupCriterias = new List<int[]>();
                #region Reading textboxes
                bool grpMemInputFound = false;
                for (int i = 1; i <= 20; i++)
                {
                    int[] gcArr = new int[7];
                    groupCriterias.Add(gcArr);
                    for (int j = 0; j < 7; j++)
                    {
                        try
                        {
                            gcArr[j] = int.Parse(grpGroupMemFilter.Controls["txtGroupMemC" + i + "G" + (j + 1)].Text);
                            grpMemInputFound = true;
                        }
                        catch
                        {
                            gcArr[j] = 0;
                        }
                    }
                }
                #endregion Reading textboxes
                bool grpMemRetainMatch = rdoGrpMemMatch.Checked;

                bool isGrpRowColFilter = chkGrpRowColFilter.Checked;
                List<string[]> groupRowCols = new List<string[]>();
                #region Reading textboxes
                bool grpRwClInputFound = false;
                for (int i = 0; i < 6; i++)
                {
                    string[] grcArr = new string[NUM_OF_GRPROWCOL];
                    groupRowCols.Add(grcArr);
                    for (int j = 0; j < NUM_OF_GRPROWCOL; j++)
                    {
                        grcArr[j] = pnlGroupRowColFilter.Controls["txtGrpRowCol" + ((char)('A' + i)) + j].Text;
                        if (grcArr[j].Length == 1)
                            grcArr[j] = "0" + grcArr[j];
                        if (grcArr[j].Length > 0)
                            grpRwClInputFound = true;
                    }
                }
                #endregion Reading textboxes
                int grpRwClMinMatch = (int)numGrpRwClMinMatch.Value;
                bool grpRwClRetainMatch = rdoGrpRwClMatch.Checked;

                #region Validation
                if (!File.Exists(inputFilePath))
                {
                    MessageBox.Show("Error: Please select Input File First!!");
                    return;
                }
                if (isBatchScanFilter && txtBatchRows.Text.Trim() == "")
                {
                    MessageBox.Show("Error: BatchScan rows are Empty!!");
                    return;
                }

                //if (isSummationFilter && txtBatchRows.Text.Trim() == "")
                //{
                //    MessageBox.Show("Error: Summation rows are Empty!!");
                //    return;
                //}
                if (isGroupMemberFilter && !grpMemInputFound)
                {
                    MessageBox.Show("Error: No user input found for Group Member Filter!!");
                    return;
                }
                if (isGrpRowColFilter && !grpRwClInputFound)
                {
                    MessageBox.Show("Error: No user input found for Group Row Column Filter!!");
                    return;
                }
                #endregion Validation

                SaveSettings();
                btnStartBatchFilter.Text = "Stop";
                btnGenStart.Enabled = false;
                btnCountingStart.Enabled = false;
                lblStatus.Text = "";
                progress.Value = progress.Minimum;
                progress.Maximum = 100;
                progressSum = 0;
                start = true;
                new Thread(() =>
                {
                    #region Main Thread

                    string logStr = "";
                    DateTime mainNow = DateTime.Now;
                    List<string[]> batchRows = new List<string[]>();
                    // List<string[]> inputRows = new List<string[]>();

                    if (isBatchScanFilter || isSummationFilter)
                        batchRows = ReadRowColumnsFromText(batchRowsText);

                    //if ()
                    //   // inputRows = ReadRowColumnsFromText(inputFilePath);
                    //batchRows = ReadRowColumnsFromText(batchRowsText);


                    #region Validation


                    if (isBatchScanFilter && batchRows.Count == 0 && batchRows[0].Length == 0)
                    {
                        start = false;
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show(this, "Input file does not contains any Rows!");
                            btnStartBatchFilter.Text = "Start";
                            btnStartBatchFilter.Enabled = true;
                        }));
                    }

                    if (isBatchScanFilter && batchRows[0].Length < targetNumCols)
                    {
                        targetNumCols = Math.Max(targetNumCols, batchRows[0].Length);
                    }

                    if (isSummationFilter && batchRows[0].Length < targetNumCols)
                    {
                        targetNumCols = Math.Max(targetNumCols, batchRows[0].Length);
                    }

                    #endregion Validation

                    var filters = new List<Func<List<string[]>, int, dynamic, List<string[]>>>();
                    List<dynamic> filterParams = new List<dynamic>();
                    List<string> filterNames = new List<string>();
                    List<bool> filterIsRetainMatch = new List<bool>();
                    Dictionary<string, FilterLogInfo> filterLogDic = new Dictionary<string, FilterLogInfo>();
                    if (isBatchScanFilter)
                    {
                        #region isBatchScanFilter


                        filters.Add(BatchScanFilter);
                        var logInfo = new FilterLogInfo();
                        filterParams.Add(new
                        {
                            batchRows = batchRows,
                            batchCols = batchCols,
                            batchMinMatch = batchMinMatch,
                            batchRetainMatch = batchRetainMatch,

                            colIndices = new List<int>(),
                            logInfo = logInfo
                        });
                        filterNames.Add("Batch Scan Filter :");

                        filterLogDic.Add("Batch Scan Filter :", logInfo);


                        #endregion isBatchScanFilter
                    }

                    if (isSummationFilter)
                    {

                        #region isSummationFilter

                        filters.Add(SummationFilter);
                        var logInfo = new FilterLogInfo();
                        filterParams.Add(new
                        {
                            batchRows = batchRows,
                            sumColumns = sumColumns,
                            numMinMatch = numMinMatch,
                            sumRetainMatched = sumRetainMatched,
                            colIndices = new List<int>(),
                            logInfo = logInfo
                        });
                        filterNames.Add("Summation Filter :");

                        filterLogDic.Add("Summation Filter :", logInfo);


                        #endregion isSummationFilter
                    }

                    if (isGroupMemberFilter)
                    {
                        #region isGroupMemberFilter

                        filters.Add(GroupMemberFilter);
                        var logInfo = new FilterLogInfo();
                        filterParams.Add(new
                        {
                            groupCriterias = groupCriterias,
                            grpMemRetainMatch = grpMemRetainMatch,
                            grpCritsRanges = new List<List<int[]>>(),
                            logInfo = logInfo
                        });
                        filterNames.Add("Group Members Selection Filter :");
                        filterIsRetainMatch.Add(grpMemRetainMatch);
                        filterLogDic.Add("Group Members Selection Filter :", logInfo);

                        #endregion isGroupMemberFilter
                    }
                    if (isGrpRowColFilter)
                    {
                        #region isGrpRowColFilter

                        filters.Add(GroupRowColumnFilter);
                        var logInfo = new FilterLogInfo();
                        filterParams.Add(new
                        {
                            groupRowCols = groupRowCols,
                            grpRwClMinMatch = grpRwClMinMatch,
                            grpRwClRetainMatch = grpRwClRetainMatch,
                            grpClmIndices = new List<int>(),
                            grpRowVals = new List<List<string>>(),
                            logInfo = logInfo
                        });
                        filterNames.Add("Group Row Colum Realignment Filter :");
                        filterIsRetainMatch.Add(grpRwClRetainMatch);
                        filterLogDic.Add("Group Row Colum Realignment Filter :", logInfo);

                        #endregion isGrpRowColFilter
                    }


                    #region Populating Threads

                    DateTime filterNow = DateTime.Now;
                    FileStream readerFS = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    string tmpFileName = Path.Combine(appDataPath, "tmpFilterFile" + DateTime.Now.Millisecond + ".txt");
                    FileStream writerFS = new FileStream(tmpFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                    List<Thread> threads = new List<Thread>();

                    for (int j = 0; j < NUM_OF_THREADS; j++)
                    {
                        Thread t = new Thread(FilterProcessorThread);
                        t.Name = j.ToString();
                        t.Start(new
                        {
                            targetNumCols = targetNumCols,
                            progressVal = 100 / (double)new FileInfo(inputFilePath).Length,
                            filters = filters,
                            filterParams = filterParams,
                            readerFS = readerFS,
                            writerFS = writerFS
                        });
                        threads.Add(t);
                    }

                    foreach (Thread t in threads)
                        t.Join();
                    readerFS.Close();
                    readerFS.Dispose();
                    writerFS.Flush();
                    writerFS.Close();
                    writerFS.Dispose();

                    double totalVirtualTimeTook = 0;
                    foreach (string fName in filterNames)
                        totalVirtualTimeTook += filterLogDic[fName].TimeTook;
                    double totalActualTimeTook = (int)DateTime.Now.Subtract(filterNow).TotalSeconds;
                    //int tmpSec = 0;
                    foreach (string fName in filterNames)
                    {
                        var logInfo = filterLogDic[fName];
                        logStr += fName + "\r\n";
                        logStr += "Input Rows: " + (logInfo.Matched + logInfo.Unmatched) + "\r\n";
                        logStr += "Matched: " + logInfo.Matched + "\r\n";
                        logStr += "Unmatched: " + logInfo.Unmatched + "\r\n";
                        //int timeTook = (int)logInfo.TimeStamp.Subtract(filterNow).TotalSeconds - tmpSec;
                        //tmpSec += timeTook;
                        logStr += "Time Took: " + (totalActualTimeTook / totalVirtualTimeTook * logInfo.TimeTook).ToString("0.000") + " sec\r\n\r\n";
                    }

                    #endregion Populating Threads

                    logStr += "\r\nTotal Time: " + (int)DateTime.Now.Subtract(mainNow).TotalSeconds + " sec";

                    bool tmpStart = start;
                    start = false;
                    this.Invoke(new Action(() =>
                    {
                        progress.Value = progress.Maximum;
                        btnStartBatchFilter.Text = "Start";
                        btnStartBatchFilter.Enabled = true;
                        btnGenStart.Enabled = true;
                        btnCountingStart.Enabled = true;
                    }));

                    if (!tmpStart)
                    {
                        if (File.Exists(tmpFileName))
                            File.Delete(tmpFileName);
                        this.Invoke(new Action(() =>
                        {
                            lblStatus.Text = "# Stopped by User!";
                        }));
                    }

                    else
                    {
                        this.Invoke(new Action(() =>
                        {
                            GC.Collect();
                            GC.GetTotalMemory(true);
                            new FrmDialog(tmpFileName, logStr).ShowDialog();
                        }));
                    }

                    #endregion Main Thread
                }).Start();
            }
            else
            {
                start = false;
                btnStartBatchFilter.Enabled = false;
            }
        }
        #endregion

        private void FilterProcessorThread(object obj)
        {
            int targetNumCols = ((dynamic)obj).targetNumCols;
            double progressVal = ((dynamic)obj).progressVal;
            List<Func<List<string[]>, int, dynamic, List<string[]>>> filters = ((dynamic)obj).filters;
            List<dynamic> filterParams = ((dynamic)obj).filterParams;
            FileStream readerFS = ((dynamic)obj).readerFS;
            FileStream writerFS = ((dynamic)obj).writerFS;

            while (start)
            {
                StringBuilder bob = this.ReadInputFile(readerFS);
                List<string[]> inputRows = this.ReadRowColumnsFromText(bob);
                int bobLen = bob.Length;
                bob.Clear();
                if (bobLen == 0 || !start)
                    break;
                for (int i = 0; i < filters.Count && inputRows.Count > 0; i++)
                {
                    inputRows = filters[i](inputRows, targetNumCols, filterParams[i]);
                }
                if (inputRows.Count > 0)
                {
                    this.WriteFilterOutput(writerFS, inputRows);
                }
                this.DoProgress(progressVal * bobLen);
            }
        }

        private ReaderWriterLockSlim batchFilterParmLocker = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim batchLogInfoParmLocker = new ReaderWriterLockSlim();

        #region MyBatchScanRegion
        private List<string[]> BatchScanFilter(List<string[]> inputRows, int targetNumCols, dynamic filterParam)
        {
            //this.Invoke(new Action(() =>
            //{
            //    lblStatus.Text = "# Batch Scan Filter";
            //}));

            DateTime now = DateTime.Now;

            List<string[]> batchRows = filterParam.batchRows;
            bool[] batchCols = filterParam.batchCols;
            bool modula = chkMoldula.Checked;
            int batchMinMatch = filterParam.batchMinMatch;
            bool batchRetainMatch = filterParam.batchRetainMatch;
            bool absolute = chkAbsolute.Checked;
            FilterLogInfo logInfo = filterParam.logInfo;
            List<string[]> outputRows = new List<string[]>();
            List<int> colIndices = null;

            try
            {
                batchFilterParmLocker.EnterWriteLock();
                colIndices = filterParam.colIndices;
                if (colIndices.Count == 0)
                {
                    for (int i = 0; i < targetNumCols; i++)
                    {
                        if (batchCols[i])
                            colIndices.Add(i);
                    }
                }
            }

            finally

            {
                batchFilterParmLocker.ExitWriteLock();
            }
            bool haveIndices = colIndices.Count > 0;

            //int m = 1;
            for (int i = 0; start && i < inputRows.Count; i++)//, m++)
            {
                string[] inputRow = inputRows[i];

                for (int j = 0; j < numDelinCount.Value; j++)
                {
                    string[] batchRow = batchRows[j];

                    int mCount = 0;

                    if (haveIndices)
                    {
                        for (int k = 0; k < colIndices.Count; k++)
                        {
                            if (inputRow[colIndices[k]] == batchRow[colIndices[k]])
                            {
                                mCount++;
                            }
                            else
                            {

                                if (modula)
                                {

                                    if (int.Parse(inputRow[k]) %10 == int.Parse(batchRow[colIndices[k]])%10)
                                    {
                                        mCount++;
                                    }

                                }
                            }
                        }

                       
                    }

               
                    else
                    {
                        for (int colum = 0; colum < numTargetCols.Value; colum++)
                        {

                            foreach (string br in batchRow)
                            {
                                if (modula)
                                {

                                    if (int.Parse(inputRow[colum]) % 10 == int.Parse(br) % 10)
                                    {
                                        mCount++;
                                    }

                                }
                                else

                                if (br == inputRow[colum])
                                {
                                    mCount++;
                                    break;
                                }


                            }
                        }
                    }


                    if (absolute)
                    {
                        if (mCount == batchMinMatch)
                        {
                            outputRows.Add(inputRow);
                            inputRows.RemoveAt(i);
                            i--;
                            break;

                        }


                    }

                    else
                    {

                        if (mCount >= batchMinMatch)
                        {
                            outputRows.Add(inputRow);
                            inputRows.RemoveAt(i);
                            i--;
                            break;
                        }
                    }


                }

                //if (m % 5000 == 0)
                //    this.DoProgress(progressVal * 5000);
            }


            //if (m % 5000 == 0)
            //    this.DoProgress(progressVal * 5000);

            try
            {
                batchLogInfoParmLocker.EnterWriteLock();
                //logInfo.TimeStamp = DateTime.Now;
                logInfo.TimeTook += DateTime.Now.Subtract(now).TotalSeconds;
                logInfo.Matched += outputRows.Count;
                logInfo.Unmatched += inputRows.Count;
            }
            finally
            {
                batchLogInfoParmLocker.ExitWriteLock();
            }
            return batchRetainMatch ? outputRows : inputRows;
        }
        #endregion

        private ReaderWriterLockSlim sumFilterParmLocker = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim sumLogInfoParmLocker = new ReaderWriterLockSlim();




///
        static bool HasExceededTargetCount(List<string[]> arrayOfArrays, int targetCount)
        {
            // HashSet to store arrays that have already been added
            HashSet<string[]> addedArrays = new HashSet<string[]>(new ArrayEqualityComparer());

            int commonCount = 0;
            foreach (string[] array in arrayOfArrays)
            {
                // Check if the array is already added
                if (addedArrays.Contains(array))
                {
                    continue; // Skip the array if it's already added
                }

                // Convert the current array to a HashSet for faster lookup
                HashSet<string> currentSet = new HashSet<string>(array);

                // Find the common elements between the current array and the previous arrays
                commonCount += currentSet.Intersect(addedArrays.SelectMany(arr => arr)).Count();

                // Add the current array to the HashSet of added arrays
                addedArrays.Add(array);

                // Break the loop early if we have already reached the target count
                if (commonCount >= targetCount)
                {
                    break;
                }
            }

            return commonCount > targetCount;
        }

        class ArrayEqualityComparer : IEqualityComparer<string[]>
        {
            public bool Equals(string[] x, string[] y)
            {
                // Check if the arrays have the same elements in the same order
                return x.SequenceEqual(y);
            }

            public int GetHashCode(string[] obj)
            {
                // Create a hash code based on the elements of the array
                int hash = 17;
                foreach (string element in obj)
                {
                    hash = hash * 23 + element.GetHashCode();
                }
                return hash;
            }
        }
       


        private List<string[]> SummationFilter(List<string[]> inputRows, int targetNumCols, dynamic filterParam)
        {

            #region DelinDeclarations
            DateTime now = DateTime.Now;


            bool batchGroup = chkBatchGroup.Checked;
            bool[] sumColumns = filterParam.sumColumns;
            bool batching = chkBatches.Checked;
            bool coupling = chkBatchCoupling.Checked;
            bool isDelinquent = chkDelinquents.Checked;
           
            bool sumRetainMatched = filterParam.sumRetainMatched;
           

            bool rangeValue = chkRangeSumVals.Checked; bool fixedValues = chkFixedVal.Checked;
            bool first2Columns = chkfirst2colSumLimit.Checked; bool first3or4Columns = chkfirst3colSumLimit.Checked; bool first4Columns = chkfirst4colSumLimit.Checked; bool first5Columns = chkfirst5colSumLimit.Checked;

            bool colSums = chkColSum.Checked;
            bool interRange = chkInterRange.Checked; bool ranges = chkRangeSums.Checked;
            bool absolute = chkAbsolutesum.Checked; bool maximise = chkMaximise.Checked; bool medium = chkMedium.Checked;
            bool guided = chkGuided.Checked;

            bool powerMatch = chkPowerMatch.Checked; bool minimum = chkMinimum.Checked;
            bool clamped = chkClampedOpt1.Checked; bool clampedOpt2 = chkClampedOpt2.Checked;

            bool powerBatch = chkPowerBatch.Checked; 
            bool powerOption = chkPowerOpt.Checked;

            bool highPowered = chkHighBatch.Checked;
            bool removeConsecutives = chkConsecutive.Checked;


            int numMinMatch = filterParam.numMinMatch;
            List<int> batchColSum = new List<int>();
            int[] inputRowColSum = new int[5];

            string[] hEGroup =  {  "06",  "08",  "16",  "18",  "26",  "28",  "36",
                                  "38",  "46",  "48",  "56",  "58",  "66",  "68"  };

            string[] hOGroup =  {  "05","07","09","15","17","19","25","27","29","35","37",
                                     "39","45","47", "49","55","57","59","65","67","69"  };
            string[] lEGroup =  {  "10",  "02",  "04",  "10",  "12",  "14",  "20",
                                     "22",  "24",  "30",  "32",  "34", "40",  "42",
                                     "44",  "50",  "52",  "54", "60",  "62",  "64",  "70"  };
            string[] lOGroup =  {  "01",  "03",  "11",  "13",  "21",  "23", "31",  "33",
                                   "41",  "43", "51",  "53",  "61",  "63"  };

            string[] oDDGroup =  {  "01", "03",  "05",  "07",  "09",
                                    "11", "13",  "15",  "17",  "19",
                                    "21", "23",  "25",  "27",  "29",
                                    "31", "33",  "35",  "37",  "39",
                                    "41", "43",  "45",  "47",  "49",
                                    "51", "53",  "55",  "57",  "59",
                                    "61","63","65","67","69"};
            string[] generalGroup = {"01", "03",  "05",  "07", "09",
                                    "11", "13",  "15",  "17",  "19",
                                    "21", "23",  "25",  "27",  "29",
                                    "31", "33",  "35",  "37",  "39",
                                    "41", "43",  "45",  "47",  "49",
                                    "51", "53",  "55",  "57",  "59",
                                    "61", "63", "65", "67",  "69" ,
                                    "02", "04",  "06",  "08",  "10", "12",
                                    "14",  "16", "18", "20",   "22",
                                    "24",  "26",  "28",  "30", "32",
                                    "34",  "36",  "38",  "40",  "42",
                                    "44",  "46",  "48",  "50",  "51",
                                    "53",  "55",  "57",  "60", "62",
                                    "64","66","68","70"};

            string[] eVNGroup =  {  "02", "04",  "06",  "08",  "10",
                               "12", "14",  "16",  "18",  "20",
                               "22", "24",  "26",  "28",  "30",
                               "32", "34",  "36",  "38",  "40",
                               "42", "44",  "46",  "48",  "50",
                               "51", "53",  "55",  "57",  "60",
                               "62","64","66","68","70"};

            string[] highGroup =  {  "05",  "07","06",  "08",  "09", "15", "16",  "17",
                                     "18", "19", "25","26", "27", "28",  "29","35", "36",
                                     "38", "37", "39", "45", "46", "48", "47", "49", "55",
                                     "56", "57", "58", "59","66",  "68", "65", "67","69"};

            string[] lowGroup =  {"01", "02", "03", "04","10","11",
                            "12",  "13",  "14",  "20",  "21",  "22",
                            "23",  "24",  "30",  "31",  "32",  "33",
                            "34",  "40",  "41",  "42",  "43",  "44",
                            "50",  "52",  "51",  "52",  "53",  "54",
                            "60",  "61",  "62","63",  "64",  "70" };


            #endregion DelinDeclarations


            List<string[]> batchRows = filterParam.batchRows; 
            FilterLogInfo logInfo = filterParam.logInfo; 
            List<string[]> outputRows = new List<string[]>(); 
            List<int> colIndices = null;


            try
            {

                sumFilterParmLocker.EnterWriteLock(); colIndices = filterParam.colIndices;
                if (colIndices.Count == 0)
                {
                    for (int i = 0; i < targetNumCols; i++)
                    {
                        if (sumColumns[i])
                            colIndices.Add(i);
                    }
                }
            }

            finally

            {
                sumFilterParmLocker.ExitWriteLock();
            }


            bool haveIndices = colIndices.Count > 0;

            for (int i = 0; i < inputRows.Count; i++)
            {

                if (start)
                {
                    #region Ranges
                    if (ranges)
                    {

                        string[] inputRow = inputRows[i];

                        string[] inputRow1 = inputRow.Take((int)numTargetCols.Value).ToArray();
                        int[] inputRowSums = Array.ConvertAll(inputRow1, int.Parse);
                        int inputRowSum = inputRowSums.Sum();

                        string[] first2Cols = inputRow.Take((int)numfirst2Cols.Value).ToArray();
                        int[] first2cols = Array.ConvertAll(first2Cols, int.Parse);
                        int first2colSum = first2cols.Sum();

                        string[] first3Cols = inputRow.Take((int)numfirst3Cols.Value).ToArray();
                        int[] first3cols = Array.ConvertAll(first3Cols, int.Parse);
                        int first3or4colSum = first3cols.Sum();

                        string[] first4Cols = inputRow.Take((int)numfirst4Cols.Value).ToArray();
                        int[] first4cols = Array.ConvertAll(first4Cols, int.Parse);
                        int first4colSum = first4cols.Sum();

                        string[] first5Cols = inputRow.Take((int)numfirst5Cols.Value).ToArray();
                        int[] first5cols = Array.ConvertAll(first5Cols, int.Parse);
                        int first5colSum = first5cols.Sum();




                        for (int row = 0; row < inputRow.Length; row++)

                        {

                            if (first2Columns && first3or4Columns && first4Columns && rangeValue)
                            {
                                if (maximise)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }


                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {

                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    if (inputRowSum >= numRangeValues.Value)
                                    {
                                        if (inputRowSum <= numRangeValuesUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                }

                                if (medium)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }


                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }


                                    if (first4colSum >= numSumOfirst4Cols.Value)
                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    else
                                    {


                                        if (inputRowSum >= numRangeValues.Value)
                                        {
                                            if (inputRowSum <= numRangeValuesUpper.Value)
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }

                                        }
                                    }
                                }


                                if (minimum)
                                {


                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (first3or4colSum >= numSumOfirst3Cols.Value)
                                            {
                                                if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                                {

                                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                                    {

                                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                        {
                                                            if (inputRowSum >= numRangeValues.Value)
                                                            {
                                                                if (inputRowSum <= numRangeValuesUpper.Value)
                                                                {
                                                                    outputRows.Add(inputRow);
                                                                    inputRows.RemoveAt(i);
                                                                    i--;
                                                                    break;
                                                                }

                                                            }
                                                        }
                                                    }
                                                }

                                            }
                                        }
                                    }
                                }

                            }

                            else
                            if (first2Columns && first3or4Columns && first4Columns)
                            {

                                if (maximise)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);

                                        }
                                        break;
                                    }


                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);

                                        }
                                        break;
                                    }

                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {

                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);

                                        }

                                    }

                                }
                                else
                                if (medium)
                                {

                                    if (guided)
                                    {

                                        if (first2colSum >= numFirst2A.Value && first2colSum <= numFirst2AUpper.Value ||
                                            first2colSum >= numFris2B.Value && first2colSum <= numFris2BUpper.Value ||
                                            first2colSum >= numFris2C.Value && first2colSum <= numFris2CUpper.Value ||
                                            first2colSum >= numFris2D.Value && first2colSum <= numFris2DUpper.Value ||
                                            first3or4colSum >= numFirst3A.Value && first3or4colSum <= numFirst3AUpper.Value ||
                                            first3or4colSum >= numFirst3B.Value && first3or4colSum <= numFirst3BUpper.Value ||
                                            first3or4colSum >= numFirst3C.Value && first3or4colSum <= numFirst3CUpper.Value ||
                                            first3or4colSum >= numFirst3D.Value && first3or4colSum <= numFirst3DUpper.Value ||
                                            first4colSum >= numFirst4A.Value && first4colSum <= numFirst4AUpper.Value ||
                                            first4colSum >= numFirst4B.Value && first4colSum <= numFirst4BUpper.Value ||
                                            first4colSum >= numFirst4C.Value && first4colSum <= numFirst4CUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                        //break;

                                    }

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }



                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;

                                        }

                                    }

                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {

                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                        break;
                                    }

                                    if (minimum)
                                    {

                                        if (first2colSum >= numSumOfirst2Cols.Value)

                                        {
                                            if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                            {
                                                if (first3or4colSum >= numSumOfirst3Cols.Value)
                                                {
                                                    if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                                    {

                                                        if (first4colSum >= numSumOfirst4Cols.Value)

                                                        {

                                                            if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                            {
                                                                outputRows.Add(inputRow);
                                                                inputRows.RemoveAt(i);
                                                                i--;
                                                                break;

                                                            }
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value && first2colSum <= numSumOfirst2ColsUpper.Value ||
                                        first3or4colSum >= numSumOfirst3Cols.Value && first3or4colSum <= numSumOfirst3ColsUpper.Value ||
                                        first4colSum >= numSumOfirst4Cols.Value && first4colSum <= numSumOfirst4ColsUpper.Value)
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;
                                    }

                                }

                            }
                            else
                            if (first2Columns && first3or4Columns && first4Columns && first5Columns)
                            {
                                if (maximise)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                }


                                if (medium)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }

                                if (minimum)

                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (first3or4colSum >= numSumOfirst3Cols.Value)
                                            {
                                                if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                                {

                                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                                    {
                                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                        {

                                                            if (first5colSum >= numSumOfirst5Cols.Value)
                                                            {
                                                                if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                                                {
                                                                    outputRows.Add(inputRow);
                                                                    inputRows.RemoveAt(i);
                                                                    i--;
                                                                    break;
                                                                }

                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }


                            }
                            else
                            if (first2Columns && first3or4Columns && first4Columns && first5Columns && rangeValue)
                            {
                                if (maximise)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {

                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {

                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (inputRowSum >= numRangeValues.Value)
                                    {
                                        if (inputRowSum <= numRangeValuesUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }

                                else
                                if (medium)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {

                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }
                                    else
                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {

                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }
                                    else
                                    if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (inputRowSum >= numRangeValues.Value)
                                    {

                                        if (inputRowSum <= numRangeValuesUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }

                                else

                                if (minimum)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (first3or4colSum >= numSumOfirst3Cols.Value)
                                            {
                                                if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                                {

                                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                                    {
                                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                        {

                                                            if (first5colSum >= numSumOfirst5Cols.Value)
                                                            {
                                                                if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                                                {

                                                                    if (inputRowSum >= numRangeValues.Value)
                                                                    {

                                                                        if (inputRowSum <= numRangeValuesUpper.Value)
                                                                        {
                                                                            outputRows.Add(inputRow);
                                                                            inputRows.RemoveAt(i);
                                                                            i--;
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                            else
                            if (first2Columns && first3or4Columns)
                            {
                                if (maximise)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }


                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {

                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                }

                                if (medium)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    else
                                    {
                                        if (first3or4colSum >= numSumOfirst3Cols.Value)
                                        {

                                            if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }
                                        }
                                    }

                                }


                                if (minimum)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                        {

                                            if (first3or4colSum >= numSumOfirst3Cols.Value)
                                            {

                                                if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }
                                            }

                                        }
                                    }
                                }


                            }
                            else
                            if (first2Columns && first4Columns)
                            {
                                if (maximise)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)
                                    {

                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                    if (first4colSum >= numSumOfirst4Cols.Value)
                                    {


                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }



                                }
                                else
                                if (medium)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)
                                    {

                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                    else
                                    if (first4colSum >= numSumOfirst4Cols.Value)
                                    {


                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                }

                                else
                                if (minimum)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (first4colSum >= numSumOfirst4Cols.Value)
                                            {
                                                if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }


                            }
                            else
                            if (first2Columns && first5Columns)
                            {

                                if (maximise)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)
                                            outputRows.Add(inputRow);

                                    }


                                    if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }


                                if (medium)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                        if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                }
                                else
                                if (minimum)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (first5colSum >= numSumOfirst5Cols.Value)
                                            {
                                                if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }

                                            }
                                        }
                                    }
                                }

                            }
                            else
                            if (first3or4Columns && first4Columns)
                            {
                                if (maximise)
                                {
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    else
                                    if (first4colSum >= numSumOfirst4Cols.Value)
                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }


                                if (medium)
                                {

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    else
                                    if (first4colSum >= numSumOfirst4Cols.Value)
                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                }

                                else

                                if (minimum)
                                {
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            if (first4colSum >= numSumOfirst4Cols.Value)
                                            {
                                                if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                            else
                            if (maximise)

                            {
                                if (first3or4Columns && first5Columns)
                                {

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                        if (first5colSum >= numSumOfirst5Cols.Value)

                                        {
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }

                                        }

                                    }
                                }

                                if (medium)
                                {
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        };
                                    }

                                    else
                                        if (first5colSum >= numSumOfirst5Cols.Value)
                                    {
                                        if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }



                                }
                                else
                                if (minimum)
                                {
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)

                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                        {
                                            if (first5colSum >= numSumOfirst5Cols.Value)
                                            {
                                                if (first5colSum <= numSumOfirst5ColsUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                            else
                            if (first4Columns && first5Columns)
                            {
                                if (maximise)
                                {
                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    if (first5colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first5colSum <= numSumOfirst4ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                }
                                else
                                if (medium)

                                {
                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }

                                    else
                                    if (first5colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first5colSum <= numSumOfirst4ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                }


                                if (minimum)
                                {
                                    if (first4colSum >= numSumOfirst4Cols.Value)

                                    {
                                        if (first4colSum <= numSumOfirst4ColsUpper.Value)
                                        {

                                            if (first5colSum >= numSumOfirst5Cols.Value)
                                            {

                                                if (first5colSum <= numSumOfirst5ColsUpper.Value)

                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                }

                            }
                            else
                            if (rangeValue && first2Columns)
                            {
                                if (maximise)
                                {

                                    if (medium)
                                    {
                                        if (first2colSum >= numSumOfirst2Cols.Value)

                                        {
                                            if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }
                                        }

                                        if (inputRowSum >= numRangeValues.Value && inputRowSum <= numRangeValuesUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }


                                    }
                                }
                                else

                                if (medium)
                                {
                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    if (inputRowSum >= numRangeValues.Value && inputRowSum <= numRangeValuesUpper.Value)
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;
                                    }


                                }


                                if (minimum)
                                {

                                    if (first2colSum >= numSumOfirst2Cols.Value)

                                    {
                                        if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                        {
                                            if (inputRowSum >= numRangeValues.Value && inputRowSum <= numRangeValuesUpper.Value)
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            if (rangeValue && first3or4Columns)
                            {

                                if (maximise)
                                {
                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);

                                        }

                                        if (inputRowSum >= numRangeValues.Value)
                                        {

                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }
                                        }
                                    }

                                }


                                if (medium)
                                {

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }


                                    else
                                    {
                                        if (inputRowSum >= numRangeValues.Value)
                                        {

                                            if (inputRowSum <= numRangeValuesUpper.Value)
                                            {

                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;
                                            }
                                        }
                                    }



                                }
                                else


                                if (minimum)
                                {

                                    if (first3or4colSum >= numSumOfirst3Cols.Value)
                                    {
                                        if (first3or4colSum <= numSumOfirst3ColsUpper.Value)
                                        {

                                            if (inputRowSum >= numRangeValues.Value)
                                            {

                                                if (inputRowSum <= numRangeValuesUpper.Value)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                            else
                            if (first2Columns)
                            {  //Setting a limit on Sum of first two columns// 

                                if (first2colSum >= numSumOfirst2Cols.Value)

                                {
                                    if (first2colSum <= numSumOfirst2ColsUpper.Value)

                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;
                                    }
                                }

                            }
                            else
                            if (first3or4Columns)
                            {  //Setting a limit on Sum of first three columns// 


                                if (first3or4colSum >= numSumOfirst3Cols.Value)

                                {
                                    if (first3or4colSum <= numSumOfirst3ColsUpper.Value)

                                    {

                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;

                                    }
                                }
                            }
                            else
                            if (first4Columns)
                            {  //Setting a limit on Sum of first three columns// 


                                if (first4colSum >= numSumOfirst4Cols.Value)

                                {
                                    if (first4colSum <= numSumOfirst4ColsUpper.Value)

                                    {

                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;

                                    }
                                }
                            }
                            else
                            if (first5Columns)
                            {  //Setting a limit on Sum of first three columns// 


                                if (first5colSum >= numSumOfirst5Cols.Value)

                                {
                                    if (first5colSum <= numSumOfirst5ColsUpper.Value)

                                    {

                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;

                                    }
                                }
                            }
                            else
                            if (rangeValue)

                            {
                                if (inputRowSum >= numRangeValues.Value && inputRowSum <= numRangeValuesUpper.Value)
                                {
                                    outputRows.Add(inputRow);
                                    inputRows.RemoveAt(i);
                                    i--;
                                    break;
                                }

                            }

                            if (fixedValues)

                            {
                                if (inputRowSum == numFixedValue.Value)
                                {
                                    outputRows.Add(inputRow);
                                    inputRows.RemoveAt(i);
                                    i--;
                                    break;
                                }

                            }
                        }


                    }
                    #endregion

                    #region batchGroup

                    if (batchGroup)

                    {
                        #region batchGroup_Declaration
                        int inputCoupleSumA = 0;
                        int inputCoupleSumB = 0;
                        int inputCoupleSumC = 0;
                        int inputCoupleSumD = 0;
                        int inputCoupleSumE = 0;

                        int batchCoupleSumA = 0;
                        int batchCoupleSumB = 0;
                        int batchCoupleSumC = 0;
                        int batchCoupleSumD = 0;
                        int batchCoupleSumE = 0;
                        int batchRowSum = 0;
                        bool isCoupleMinus = chkIRMinus.Checked;
                        bool isLimit = chkLimitCount.Checked;
                        #endregion batchGroup_Declaration


                        string[] inputRow = inputRows[i];

                        var inputRow1 = inputRow.Take((int)numTargetCols.Value).ToArray();

                        int[] inputRowSums = Array.ConvertAll(inputRow1, int.Parse);

                        int inputRowSum = inputRowSums.Sum();


                        for (int j = 0; j < numDelinCount.Value; j++)
                        {
                            #region batching_coupling

                            if (batching || coupling)
                            {

                                string[] batchRow = batchRows[j];

                                if (batching)
                                {

                                    string[] batchRow1 = batchRow.Take((int)numTargetCols.Value).ToArray();

                                    int[] batchRowSums = Array.ConvertAll(batchRow1, int.Parse);

                                    batchRowSum = batchRowSums.Sum();


                                    if (batchRowSum == inputRowSum)
                                    {

                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;
                                    }
                                }

                                if (coupling)
                                {
                                    if (isCoupleMinus)
                                    {

                                        inputCoupleSumA = Math.Abs(int.Parse(inputRow[0])) - Math.Abs(int.Parse(inputRow[1]));
                                        inputCoupleSumB = Math.Abs(int.Parse(inputRow[1])) - Math.Abs(int.Parse(inputRow[2]));
                                        if (inputRow.Length >= 4)
                                        {

                                            inputCoupleSumC = Math.Abs(int.Parse(inputRow[2])) - Math.Abs(int.Parse(inputRow[3]));


                                            if (inputRow.Length >= 5)
                                            {
                                                inputCoupleSumD = Math.Abs(int.Parse(inputRow[3])) - Math.Abs(int.Parse(inputRow[4]));

                                                if (inputRow.Length >= 6)
                                                {
                                                    inputCoupleSumE =
                                                    Math.Abs(int.Parse(inputRow[4])) - Math.Abs(int.Parse(inputRow[5]));
                                                }
                                            }
                                        }


                                        batchCoupleSumA = Math.Abs(int.Parse(batchRow[0])) - Math.Abs(int.Parse(batchRow[1]));
                                        batchCoupleSumB = int.Parse(batchRow[1]) - int.Parse(batchRow[2]);
                                        if (batchRow.Length >= 4)
                                        {
                                            batchCoupleSumC = Math.Abs(int.Parse(batchRow[2])) - Math.Abs(int.Parse(batchRow[3]));
                                        }

                                        if (batchRow.Length >= 5)
                                        {
                                            batchCoupleSumD = Math.Abs(int.Parse(batchRow[3])) - Math.Abs(int.Parse(batchRow[4]));
                                        }

                                        if (batchRow.Length >= 6)
                                        {

                                            batchCoupleSumE = Math.Abs(int.Parse(batchRow[4])) - Math.Abs(int.Parse(batchRow[5]));
                                        }

                                    }
                                    else
                                    {
                                        inputCoupleSumA = int.Parse
                                        (inputRow[0]) + int.Parse(inputRow[1]);

                                        inputCoupleSumB = int.Parse(inputRow[1]) + int.Parse(inputRow[2]);

                                        if (inputRow.Length >= 4)
                                        {
                                            inputCoupleSumC = int.Parse(inputRow[2]) + int.Parse(inputRow[3]);
                                            if (inputRow.Length >= 5)
                                            {

                                                inputCoupleSumD = int.Parse(inputRow[3]) + int.Parse(inputRow[4]);
                                            }
                                            if (inputRow.Length >= 6)
                                            {
                                                inputCoupleSumE = int.Parse(inputRow[4]) + int.Parse(inputRow[5]);
                                            }
                                        }

                                        batchCoupleSumA = int.Parse(batchRow[0]) + int.Parse(batchRow[1]);

                                        batchCoupleSumB = int.Parse(batchRow[1]) + int.Parse(batchRow[2]);

                                        if (batchRow.Length >= 4)
                                        {

                                            batchCoupleSumC = int.Parse(batchRow[2]) + int.Parse(batchRow[3]);


                                            if (batchRow.Length >= 5)
                                            {
                                                batchCoupleSumD = int.Parse(batchRow[3]) + int.Parse(batchRow[4]);


                                                if (batchRow.Length >= 6)
                                                {

                                                    batchCoupleSumE = int.Parse(batchRow[4]) + int.Parse(batchRow[5]);
                                                }
                                            }
                                        }


                                    }

                                    int count = 0;


                                    if (inputCoupleSumA == batchCoupleSumA)

                                    {
                                        count++;

                                    }
                                    if (inputCoupleSumB == batchCoupleSumB)

                                    {
                                        count++;

                                    }

                                    if (inputRow.Length >= 4)
                                    {

                                        if (inputCoupleSumC == batchCoupleSumC)

                                        {
                                            count++;

                                        }
                                    }

                                    if (inputRow.Length >= 5)
                                    {

                                        if (inputCoupleSumD == batchCoupleSumD)
                                        {
                                            count++;

                                        }
                                    }

                                    if (inputRow.Length >= 6)
                                    {
                                        if (inputCoupleSumE == batchCoupleSumE)
                                        {


                                            count++;
                                        }
                                    }


                                    if (absolute)
                                    {
                                        if (count == numMinMatch)// Here for absulte equality
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }
                                    }
                                    else
                                    {

                                        if (count >= numMinMatch)// Here for absulte equality
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }
                                }


                            }

                            #endregion batching_coupling

                            #region DelinQuents

                            if (isDelinquent)

                            {

                                #region isDelinDeclarations
                                bool repeated = HasExceededTargetCount(outputRows, 1);
                               // List<string[]> newBatchRows = new List<string[]> { };
                                //List<string> newBatchRow = new List<string> { };
                                bool hE = chkHE.Checked;
                                bool hO = chkHO.Checked;
                                bool lO = chkLO.Checked;
                                bool lE = chkLE.Checked;
                                bool loW = chkLow.Checked;
                                bool higH = chkHigh.Checked;
                                bool oDD = chkoDD.Checked;
                                bool eVN = chkEVN.Checked;
                                bool patternExt = chkPatternsExt.Checked;
                                bool isModula = chkModUnmod.Checked;
                                bool isCoupled_Rows = chkCoupled_Rows.Checked;
                                bool isUnCoupled_Rows = chkUnCoupled_Rows.Checked;
                                bool isolateOddsEvens = ckhIsolateOddsEvens.Checked;
                            

                                int hOCount = 0;
                                int hECount = 0;
                                int lOCount = 0;
                                int lECount = 0;
                                int highCount = 0;
                               
                                int loWCount = 0;
                                int oDDCount = 0;
                                int eVNCount = 0;
                                
                                #endregion isDelinDeclarations

                                string[] batchRow = batchRows[j];

                                if (isCoupled_Rows)
                                {
                                    #region Modular Coupled
                                    if (isModula)
                                    {
                                        if (higH)
                                        {
                                            highCount = 0;
                                            hOCount = 0;
                                            hECount = 0;

                                            for (int k = 0; k < numTargetCols.Value; k++)  /// count all coloumns in batxhRow 
                                            {
                                                foreach (string btch in batchRow.Intersect(highGroup))

                                                {

                                                    if (int.Parse(inputRow1[k]) % 10 == int.Parse(btch) % 10)
                                                    {
                                                        if (isolateOddsEvens)
                                                        {
                                                            if (int.Parse(inputRow1[k]) % 2 == 0)
                                                            {
                                                                hECount++;

                                                                highCount++;

                                                                break;
                                                            }

                                                            else

                                                            {
                                                                if (int.Parse(inputRow1[k]) % 2 == 1)
                                                                {

                                                                    hOCount++;
                                                                    highCount++;

                                                                    break;
                                                                }

                                                            }
                                                        }
                                                        else
                                                        {
                                                            highCount++;
                                                            break;
                                                        }
                                                    }

                                                }

                                            }

                                            for (int tk = 0; tk < numDelinCount1.Value; tk++)
                                            {

                                                loWCount = 0;
                                                lECount = 0;
                                                lOCount = 0;

                                                string[] batchRow1 = batchRows[tk];

                                                for (int h = 0; h < numTargetCols.Value; h++)
                                                {
                                                    foreach (string btch in batchRow1.Intersect(lowGroup))

                                                    {
                                                        if (int.Parse(inputRow1[h]) == int.Parse(btch))
                                                        {

                                                            if (isolateOddsEvens)
                                                            {
                                                                if (int.Parse(btch) % 2 == 0)
                                                                {
                                                                    lECount++;
                                                                    loWCount++;

                                                                    break;
                                                                }

                                                                else
                                                                {
                                                                    if (int.Parse(btch) % 2 == 1)
                                                                    {
                                                                        lOCount++;
                                                                        loWCount++;

                                                                        break;
                                                                    }
                                                                }
                                                            }


                                                            else
                                                            {
                                                                loWCount++;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    if (highCount==numHighCount0.Value)
                                                    {
                                                        if (loWCount == (int)numLowCount0.Value)
                                                        {
                                                            break;
                                                        }
                                                    }

                                                }



                                                if (absolute)
                                                {

                                                    if (highCount == numHighCount0.Value)
                                                    {

                                                        if (hECount >= numHECount.Value || hOCount >= numHOCount.Value)
                                                        {

                                                            if (loWCount == numLowCount0.Value)
                                                            {
                                                                if (lECount >= numLECount.Value || lOCount >= numLOCount.Value)
                                                                {

                                                                    if (!outputRows.Contains(inputRow))
                                                                    {

                                                                        outputRows.Add(inputRows[i]);
                                                                        inputRows.RemoveAt(i);
                                                                        --i;
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }

                                                }
                                                else
                                                {
                                                    if (highCount == numHighCount0.Value)// || highCount == targetNumCols)
                                                    {

                                                        if (loWCount == numLowCount0.Value || highCount == targetNumCols)
                                                        {
                                                            if (!outputRows.Contains(inputRow))
                                                            {

                                                                outputRows.Add(inputRows[i]);
                                                                inputRows.RemoveAt(i);
                                                                --i;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        #region Temporatr_Options
                                        if (loW)
                                        {
                                            
                                            int highCount1 = 0;
                                            loWCount = 0;
                                            int loWCount1 = 0;
                                            lECount = 0;
                                            lOCount = 0;
                                            string[] batchrow0 = null;
                                            List<string> alreadySeenLow = new List<string>();
                                            List<string> alreadySeenHigh = new List<string>();
                                     


                                            foreach (string lowInput in inputRow1.Intersect(lowGroup))
                                            {
                                                if (numLowCount0.Value != 0)
                                                {
                                                    foreach (string btchLow0 in batchRow.Intersect(lowGroup))
                                                    {
                                                        if (int.Parse(lowInput)  == int.Parse(btchLow0))
                                                        {
                                                            if (!alreadySeenLow.Contains(lowInput))
                                                            {
                                                                if (isolateOddsEvens)
                                                                {
                                                                    if (int.Parse(lowInput) % 2 == 0)
                                                                    {
                                                                        lECount++;
                                                                        loWCount++;
                                                                        alreadySeenLow.Add(lowInput);
                                                                        break;
                                                                    }
                                                                    else
                                                                    {
                                                                        if (int.Parse(lowInput) % 2 == 1)
                                                                        {

                                                                            lOCount++;
                                                                            loWCount++;
                                                                            alreadySeenLow.Add(lowInput);
                                                                            break;
                                                                        }

                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    loWCount++;
                                                                    alreadySeenLow.Add(lowInput);
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }

                                                if (loWCount == numLowCount0.Value)
                                                {
                                                    break;
                                                }
                                            }



                                            if (alreadySeenLow.Count >= 3)
                                            {
                                                alreadySeenLow.Clear();
                                            }
                                            for (int td = 0; td < numDelinCount1.Value; td++)
                                            {
                                                if (loWCount == numLowCount0.Value)
                                                { 

                                                    string[] btchLow = batchRows[td];
                                                    foreach (string lowInput1 in inputRow1.Intersect(lowGroup))
                                                    {
                                                        if (alreadySeenLow.Contains(lowInput1))
                                                        {
                                                            continue;
                                                        }

                                                        foreach (string btchLow1 in btchLow.Intersect(lowGroup))
                                                        {

                                                            if (!alreadySeenLow.Contains(btchLow1))
                                                            {
                                                                if (int.Parse(lowInput1) == int.Parse(btchLow1))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(lowInput1) % 2 == 0)
                                                                        {
                                                                            lECount++;
                                                                            loWCount1++;
                                                                            alreadySeenLow.Add(lowInput1);
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(lowInput1) % 2 == 1)
                                                                            {

                                                                                lOCount++;
                                                                                loWCount1++;
                                                                                alreadySeenLow.Add(lowInput1);
                                                                                break;
                                                                            }

                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        loWCount1++;
                                                                        alreadySeenLow.Add(lowInput1);
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        if (loWCount1 >= numLowCount1.Value)
                                                        {
                                                            break;

                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    alreadySeenLow.Clear();
                                                    string[] btchLow = batchRows[td];
                                                    foreach (string lowInput1 in inputRow1.Intersect(lowGroup))
                                                    {
                                                        foreach (string btchLow1 in btchLow.Intersect(lowGroup))
                                                        {

                                                            if (!alreadySeenLow.Contains(lowInput1))
                                                            {
                                                                if (int.Parse(lowInput1) == int.Parse(btchLow1))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(lowInput1) % 2 == 0)
                                                                        {
                                                                            lECount++;
                                                                            loWCount1++;
                                                                            alreadySeenLow.Add(lowInput1);
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(lowInput1) % 2 == 1)
                                                                            {

                                                                                lOCount++;
                                                                                loWCount1++;
                                                                                alreadySeenLow.Add(lowInput1);
                                                                                break;
                                                                            }

                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        loWCount1++;
                                                                        alreadySeenLow.Add(lowInput1);
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }


                                                        if (loWCount1 >= numLowCount1.Value)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }

                                                if (alreadySeenLow.Count == targetNumCols)
                                                {
                                                    break;
                                                }

                                            }



                                            highCount = 0;
                                            hOCount = 0;
                                            hECount = 0;
                                            highCount1 = 0;

                                            if (alreadySeenHigh.Count >= 3)
                                            {
                                                alreadySeenHigh.Clear();
                                            }
                                            for (int tk = 0; tk < numDelinCount2.Value; tk++)
                                            {
                                                string[] batchRow0 = batchRows[tk];
                                                batchrow0 = batchRow0;

                                                foreach (string inputHigh1 in inputRow1.Intersect(highGroup))
                                                {
                                                    foreach (string btchHigh in batchRow0.Intersect(highGroup))
                                                    {
                                                        if (!alreadySeenHigh.Contains(inputHigh1))
                                                        {
                                                            if (int.Parse(inputHigh1) == int.Parse(btchHigh))
                                                            {
                                                                if (isolateOddsEvens)
                                                                {
                                                                    if (int.Parse(inputHigh1) % 2 == 0)
                                                                    {
                                                                        hECount++;
                                                                        highCount++;
                                                                        alreadySeenHigh.Add(inputHigh1);
                                                                        break;
                                                                    }
                                                                    else
                                                                    {
                                                                        if (int.Parse(inputHigh1) % 2 == 1)
                                                                        {
                                                                            hOCount++;
                                                                            highCount++;
                                                                            alreadySeenHigh.Add(inputHigh1);
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (highCount != numHighCount0.Value)
                                                                    {
                                                                        highCount++;

                                                                        alreadySeenHigh.Add(inputHigh1);
                                                                        break;

                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }

                                                    if (highCount == numHighCount0.Value)
                                                    {
                                                        break;
                                                    }
                                                }
                                                if (highCount == numHighCount0.Value)
                                                {
                                                    break;
                                                }
                                            }

                                            for (int tt = 0; tt < numDelinCount3.Value; tt++)

                                            {
                                                if (highCount == numHighCount0.Value)
                                                {

                                                    // int highInputsCount = 0;
                                                    string[] batchRow1 = batchRows[tt];
                                                    foreach (string inputHigh2 in inputRow1.Intersect(highGroup))
                                                    {
                                                        foreach (string btchHigh2 in batchRow1.Intersect(highGroup))
                                                        {
                                                            if (!alreadySeenHigh.Contains(btchHigh2))
                                                            {
                                                                if (int.Parse(inputHigh2) == int.Parse(btchHigh2))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(inputHigh2) % 2 == 0)
                                                                        {
                                                                            hECount++;
                                                                            highCount1++;
                                                                            alreadySeenHigh.Add(inputHigh2);
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(inputHigh2) % 2 == 1)
                                                                            {
                                                                                hOCount++;
                                                                                highCount1++;
                                                                                alreadySeenHigh.Add(inputHigh2);
                                                                                break;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        highCount1++;
                                                                        alreadySeenHigh.Add(inputHigh2);
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }

                                                    }


                                                }
                                                else
                                                {
                                                    alreadySeenHigh.Clear();
                                                    string[] batchRow1 = batchRows[tt];
                                                    foreach (string inputHigh2 in inputRow1.Intersect(highGroup))
                                                    {
                                                        if (alreadySeenHigh.Contains(inputHigh2))
                                                        {
                                                            continue;
                                                        }
                                                        foreach (string btchHigh2 in batchRow1.Intersect(highGroup))
                                                        {
                                                            if (!alreadySeenHigh.Contains(btchHigh2))
                                                            {
                                                                if (int.Parse(inputHigh2) == int.Parse(btchHigh2))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(inputHigh2) % 2 == 0)
                                                                        {
                                                                            hECount++;
                                                                            highCount1++;
                                                                            alreadySeenHigh.Add(inputHigh2);
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(inputHigh2) % 2 == 1)
                                                                            {
                                                                                hOCount++;
                                                                                highCount1++;
                                                                                alreadySeenHigh.Add(inputHigh2);
                                                                                break;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        highCount1++;
                                                                        alreadySeenHigh.Add(inputHigh2);
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        if (highCount1 >= numHighCount1.Value)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                                if (highCount1 >= numHighCount1.Value)
                                                {
                                                    break;
                                                }
                                            }

                                            if (absolute)
                                            {
                                                if (loWCount == numLowCount0.Value && loWCount1 >= numLowCount1.Value
                                                    || highCount == numHighCount0.Value && highCount1 >= numHighCount1.Value)
                                                {

                                                    if (lOCount >= numLOCount.Value && lECount >= numLECount.Value
                                                       || hOCount >= numHOCount.Value && hECount >= numHECount.Value)
                                                    {

                                                        if (loWCount1 + loWCount >= numLowCount1.Value + numLowCount0.Value && loWCount1 > numLowCount1.Value
                                                           || highCount1 + highCount >= numHighCount1.Value + numHighCount0.Value && highCount1 > numHighCount1.Value
                                                           || loWCount1 + loWCount == numLowCount1.Value + numLowCount0.Value && highCount1 >= numHighCount1.Value 
                                                           || highCount1 + highCount == numHighCount1.Value + numHighCount0.Value &&  loWCount1 >= numLowCount1.Value)
                                                        {

                                                            if (!outputRows.Contains(inputRow1))
                                                            {
                                                                outputRows.Add(inputRows[i]);
                                                                inputRows.RemoveAt(i);
                                                                --i;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            else

                                            {
                                                if (!outputRows.Contains(inputRow1))
                                                {

                                                    if (loWCount == numLowCount0.Value && numLowCount0.Value != 0)// || highCount == numHighCount0.Value)
                                                    {

                                                        if (loWCount1 > numLowCount1.Value || loWCount1 == numLowCount1.Value && highCount1 == numHighCount1.Value)
                                                        {

                                                            outputRows.Add(inputRow1);
                                                            inputRows.RemoveAt(i);
                                                            --i;
                                                            break;
                                                        }
                                                    }

                                                    else
                                                    {

                                                        if (highCount == numHighCount0.Value && numHighCount0.Value != 0)// || loWCount == numLowCount0.Value)
                                                        {
                                                            if (highCount1 > numHighCount1.Value || highCount1 == numHighCount1.Value && loWCount1 == numLowCount1.Value)
                                                            {
                                                                outputRows.Add(inputRow1);
                                                                inputRows.RemoveAt(i);
                                                                --i;
                                                                break;

                                                            }
                                                        }
                                                    }

                                                }
                                            }
                                            
                                        }

                                        #endregion Temporary_Options
                                    }
                                    #endregion Modular_Coupled

                                    else
                                    {
                                        #region UnModular Coupled                                   

                                        if (higH)
                                        {

                                            //MainInputFiltering mainInputFiltering = new MainInputFiltering();
                                            //{

                                            //    List<string[]> mainInputResults = mainInputFiltering.InputRowsFilterer(
                                            //      inputRows,
                                            //      batchRows,
                                            //      batchRows,
                                            //      batchRows,
                                            //      highGroup,
                                            //      highGroup,
                                            //      lowGroup,
                                            //      (int)numDelinCount.Value,
                                            //      (int)numDelinCount1.Value,
                                            //      (int)numDelinCount2.Value,
                                            //      (int)numHighCount0.Value,
                                            //      (int)numHighCount1.Value,
                                            //      (int)numLowCount0.Value);



                                            //    foreach (string[] array in mainInputResults)
                                            //    {


                                            //        if (absolute)
                                            //        {

                                            //            outputRows.Add(array);
                                            //            inputRows.RemoveAt(i);
                                            //            --i;
                                            //            break;


                                            //        }
                                            //    }




                                            //}


                                                for (int tp = 0; tp < numDelinCount2.Value; tp++) //// numDelinCount.Value; tk++)
                                                {
                                                    string[] batchRow2 = batchRows[tp];
                                                    
                                                    //bool containsCommon2 = ContainsCommons(batchRow, batchRow1);
                                                    //bool checkHighGroup2 = ChecksHighGroupCount(highGroup, inputRow, (int)numMinMatch);


                                                   //int lowCount1 = 0;

                                                    //if (!containsCommon)
                                                    //{

                                                    //    for (int k = 0; k < batchRow1.Length; k++)
                                                    //    {
                                                    //        foreach (string low1 in inputRow.Intersect(lowGroup))
                                                    //        {
                                                    //            if (int.Parse(low1) == int.Parse(batchRow1[k]))
                                                    //            {

                                                    //                lowCount1++;
                                                    //                break;


                                                    //            }
                                                    //        }
                                                    //    }
                                                    //}
                                                }
                                                ///
                                                    if (absolute)
                                                {
                                                    //if (highCount + highCount0 == numHighCount1.Value)
                                                    //{

                                                    //    outputRows.Add(inputRow);
                                                    //    // inputRows.RemoveAt(i);

                                                    //    // --i;
                                                    //    break;
                                                    //}
                                                }
                                            }



                                        if (loW)
                                        {
                                            HashSet<List<string[]>> filteredInputRoW = new HashSet<List<string[]>>() { };
                                            HashSet<string> inputROWs = new HashSet<string>();
                                            HashSet<string[]> inpuTrow = new HashSet<string[]>();
                                            HashSet<string[]> batchRow0 = new HashSet<string[]>(batchRows.Take((int)numDelinCount.Value));
                                            HashSet<string[]> batchRow1 = new HashSet<string[]>(batchRows.Take((int)numDelinCount1.Value));
                                            HashSet<string[]> batchRow2 = new HashSet<string[]>(batchRows.Take((int)numDelinCount2.Value));
                                            HashSet<string[]> batchRow3 = new HashSet<string[]>(batchRows.Take((int)numDelinCount3.Value));
                                            HashSet<string> highGroup1 = new HashSet<string>(highGroup);
                                            HashSet<string> highGroup2 = new HashSet<string>(highGroup);
                                            HashSet<string> lowGroup1 = new HashSet<string>(lowGroup);
                                            HashSet<string> lowGroup2 = new HashSet<string>(lowGroup);
                                            highCount = 0;
                                            int highCount1 = 0;
                                            loWCount = 0;
                                            int loWCount1 = 0;
                                            foreach (string[] inputrow in inputRows)
                                            {
                                                inputROWs.UnionWith(inputrow.Take(targetNumCols));
                                                                                               
                                            }
                                            foreach (string[] btchLow in batchRow0)
                                            {
                                                lowGroup1.UnionWith(btchLow);

                                            }

                                            foreach (string lowInput in inputROWs)
                                            {
                                                if (numLowCount0.Value != 0)
                                                {
                                                    foreach (string[] btchLow in batchRow0)
                                                    {
                                                        foreach (string lowString1 in btchLow)
                                                        {
                                                            if (int.Parse(lowInput) == int.Parse(lowString1))
                                                            {
                                                                if (isolateOddsEvens)
                                                                {
                                                                    if (int.Parse(lowInput) % 2 == 0)
                                                                    {
                                                                        lECount++;
                                                                        loWCount++;
                                                                        break;
                                                                    }
                                                                    else
                                                                    {
                                                                        if (int.Parse(lowInput) % 2 == 1)
                                                                        {

                                                                            lOCount++;
                                                                            loWCount++;
                                                                            break;
                                                                        }

                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    loWCount++;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }

                                                if (loWCount == numLowCount0.Value)
                                                {
                                                    break;
                                                }
                                            }


                                            for (int td = 0; td < numDelinCount1.Value; td++)
                                            {
                                                if (loWCount == numLowCount0.Value)
                                                {
                                                    string[] btchLow = batchRows[td];
                                                    foreach (string lowInput1 in inputROWs.Intersect(lowGroup1))
                                                    {

                                                        foreach (string[] btchLow2 in batchRow1)
                                                        {
                                                            foreach (string lowString1 in btchLow2)
                                                            {
                                                                if (int.Parse(lowInput1) == int.Parse(lowString1))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(lowInput1) % 2 == 0)
                                                                        {
                                                                            lECount++;
                                                                            loWCount1++;
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(lowInput1) % 2 == 1)
                                                                            {

                                                                                lOCount++;
                                                                                loWCount1++;
                                                                                break;
                                                                            }

                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        loWCount1++;
                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        if (loWCount1 >= numLowCount1.Value)
                                                        {
                                                            break;

                                                        }
                                                    }
                                                }
                                                else

                                                {

                                                   foreach (string lowInput1 in inputROWs.Intersect(lowGroup1))
                                                    {
                                                        foreach (string[] btchLow1 in batchRow1)
                                                        {
                                                            foreach (string lowString2 in btchLow1)
                                                            {
                                                                if (int.Parse(lowInput1) == int.Parse(lowString2))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(lowInput1) % 2 == 0)
                                                                        {
                                                                            lECount++;
                                                                            loWCount1++;
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(lowInput1) % 2 == 1)
                                                                            {

                                                                                lOCount++;
                                                                                loWCount1++;
                                                                                break;
                                                                            }

                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        loWCount1++;
                                                                        break;
                                                                    }
                                                                }
                                                            }

                                                            if (loWCount1 >= numLowCount1.Value)
                                                            {
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            

                                                highCount = 0;
                                                hOCount = 0;
                                                hECount = 0;
                                                highCount1 = 0;


                                                for (int tk = 0; tk < numDelinCount2.Value; tk++)
                                                {

                                                    foreach (string inputHigh1 in inputROWs.Intersect(highGroup1))
                                                    {
                                                        foreach (string[] btchHigh in batchRow2)
                                                        {
                                                            foreach (string highString1 in btchHigh)
                                                            {
                                                                if (int.Parse(inputHigh1) == int.Parse(highString1))
                                                                {
                                                                    if (isolateOddsEvens)
                                                                    {
                                                                        if (int.Parse(inputHigh1) % 2 == 0)
                                                                        {
                                                                            hECount++;
                                                                            highCount++;
                                                                            break;
                                                                        }
                                                                        else
                                                                        {
                                                                            if (int.Parse(inputHigh1) % 2 == 1)
                                                                            {
                                                                                hOCount++;
                                                                                highCount++;
                                                                                break;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        if (highCount != numHighCount0.Value)
                                                                        {
                                                                            highCount++;

                                                                            break;

                                                                        }
                                                                    }
                                                                }
                                                            }

                                                        }

                                                        //if (highCount == numHighCount0.Value)
                                                        //{
                                                        //    break;
                                                        //}
                                                    }
                                                    if (highCount == numHighCount0.Value)
                                                    {
                                                        break;
                                                    }
                                                }

                                                 for (int tt = 0; tt < numDelinCount3.Value; tt++)

                                                {
                                                    if (highCount == numHighCount0.Value)
                                                    {

                                                        // int highInputsCount = 0;

                                                        foreach (string inputHigh2 in inputROWs.Intersect(highGroup2))
                                                        {
                                                            foreach (string[] btchHigh in batchRow3)
                                                            {
                                                                foreach (string highString2 in btchHigh)
                                                                {
                                                                    if (int.Parse(inputHigh2) == int.Parse(highString2))
                                                                    {
                                                                        if (isolateOddsEvens)
                                                                        {
                                                                            if (int.Parse(inputHigh2) % 2 == 0)
                                                                            {
                                                                                hECount++;
                                                                                highCount1++;
                                                                                break;
                                                                            }
                                                                            else
                                                                            {
                                                                                if (int.Parse(inputHigh2) % 2 == 1)
                                                                                {
                                                                                    hOCount++;
                                                                                    highCount1++;
                                                                                    break;
                                                                                }
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            highCount1++;
                                                                            break;
                                                                        }
                                                                    }

                                                                }
                                                            }
                                                        }
                                                    }

                                                    else

                                                    {

                                                        foreach (string inputHigh2 in inputROWs.Intersect(highGroup1))
                                                        {

                                                            foreach (string[] btchHigh in batchRow3)
                                                            {
                                                                foreach (string highString2 in btchHigh)
                                                                {
                                                                    if (int.Parse(inputHigh2) == int.Parse(highString2))
                                                                    {
                                                                        if (isolateOddsEvens)
                                                                        {
                                                                            if (int.Parse(inputHigh2) % 2 == 0)
                                                                            {
                                                                                hECount++;
                                                                                highCount1++;

                                                                                break;
                                                                            }
                                                                            else
                                                                            {
                                                                                if (int.Parse(inputHigh2) % 2 == 1)
                                                                                {
                                                                                    hOCount++;
                                                                                    highCount1++;

                                                                                    break;
                                                                                }
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            highCount1++;
                                                                            break;
                                                                        }
                                                                    }

                                                                }
                                                                if (highCount1 >= numHighCount1.Value)
                                                                {
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                        if (highCount1 >= numHighCount1.Value)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }


                                                if (absolute)
                                                {
                                                    if (loWCount == numLowCount0.Value || highCount == numHighCount0.Value)
                                                    {

                                                        if (lOCount >= numLOCount.Value && lECount >= numLECount.Value
                                                           || hOCount >= numHOCount.Value && hECount >= numHECount.Value)
                                                        {

                                                            if (loWCount1 + loWCount >= numLowCount1.Value + numLowCount0.Value && loWCount1 > numLowCount1.Value
                                                               || highCount1 + highCount >= numHighCount1.Value + numHighCount0.Value && highCount1 > numHighCount1.Value
                                                               || loWCount1 + loWCount == numLowCount1.Value + numLowCount0.Value && highCount1 > numHighCount1.Value
                                                               || highCount1 + highCount == numHighCount1.Value + numHighCount0.Value && loWCount1 > numLowCount1.Value)
                                                            {

                                                                if (!outputRows.Contains(inputRow1.Take(targetNumCols)))
                                                                {
                                                                    outputRows.Add(inputRows[i]);
                                                                    inputRows.RemoveAt(i);
                                                                    --i;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            
                                        #endregion UnModular Coupled
                                    }

                                }

                                #region isUnCoupled
                               
                                #endregion isUnCoupled
                            }

                            

                        }

                        #endregion DelinQuents

                    }
                    #endregion batches
                }
                //Remove cosecutives intergers

                if (removeConsecutives)
                {
                    for (int k = 0; k < inputRows.Count; k++)
                    {
                        string[] inputRow = new string[(int)numTargetCols.Value];
                        inputRow = inputRows[i];
                        bool isConsecutive = false;
                        for (int j = 0; j < inputRow.Length - 1; j++)
                        {
                            if (int.Parse(inputRow[j]) + 1 == int.Parse(inputRow[j + 1]))
                            {
                                isConsecutive = true;
                                break;
                            }
                        }
                        if (!isConsecutive)
                        {
                            outputRows.Add(inputRow);
                            inputRows.RemoveAt(i);
                            --i;
                        }
                    }
                }
                //bool repeated1 = HasExceededTargetCount(outputRows, 1);
                ////if (repeated1)
                ////{
                ////    outputRows.Remove(inputRows[i]);
                ////}

                #region InterRange
                if (interRange)

                {
                    bool isInterRangeLimit = chkLimitCount.Checked;
                    bool isIRMinus = chkIRMinus.Checked;
                    bool isLimit = chkLimitCount.Checked;

                    if (!haveIndices)
                    {

                        int batchColsum0 = 0;
                        int batchColsum1 = 0;
                        int batchColsum2 = 0;
                        int batchColsum3 = 0;
                        int batchColsum4 = 0;
                        int batchColsum5 = 0;

                        int inputColsum0 = 0;
                        int inputColsum1 = 0;
                        int inputColsum2 = 0;
                        int inputColsum3 = 0;
                        int inputColsum4 = 0;
                        int inputColsum5 = 0;

                        for (i = 0; i < inputRows.Count; i++)
                        {
                            string[] inputRow = new string[(int)numTargetCols.Value];
                            string[] inputRow1 = new string[(int)numTargetCols.Value];
                            string[] batchRow1 = new string[(int)numTargetCols.Value];
                            string[] batchRow2 = new string[(int)numTargetCols.Value];
                            i = 0;
                            int iR = 0;
                            //int mcount = 0;


                            for (int kk = batchRows.Count - 2; kk > -1; kk--)
                            {

                                // int mcount = 0;
                                batchRow2 = batchRows[kk];

                                batchRow1 = batchRows[kk + 1];


                                batchColsum0 = int.Parse(batchRow1[0]) + int.Parse(batchRow2[0]);
                                batchColsum1 = int.Parse(batchRow1[1]) + int.Parse(batchRow2[1]);

                                if (batchRow1.Length >= 3)
                                {
                                    batchColsum2 = int.Parse(batchRow1[2]) + int.Parse(batchRow2[2]);

                                }

                                if (batchRow1.Length >= 4)
                                {
                                    batchColsum3 = int.Parse(batchRow1[3]) + int.Parse(batchRow2[3]);

                                }

                                if (batchRow1.Length >= 5)
                                {
                                    batchColsum4 = int.Parse(batchRow1[4]) + int.Parse(batchRow2[4]);

                                }

                                if (batchRow1.Length == 6)
                                {
                                    batchColsum5 = int.Parse(batchRow1[5]) + int.Parse(batchRow2[5]);

                                }

                                if (isIRMinus)
                                {
                                    batchColsum0 = Math.Abs(int.Parse(batchRow1[0])) - Math.Abs(int.Parse(batchRow2[0]));
                                    batchColsum1 = int.Parse(batchRow1[1]) - int.Parse(batchRow2[1]);

                                    if (batchRow1.Length >= 3)
                                    {
                                        batchColsum2 = Math.Abs(int.Parse(batchRow1[2])) - Math.Abs(int.Parse(batchRow2[2]));

                                    }

                                    if (batchRow1.Length >= 4)
                                    {
                                        batchColsum3 = Math.Abs(int.Parse(batchRow1[3])) - Math.Abs(int.Parse(batchRow2[3]));

                                    }

                                    if (batchRow1.Length >= 5)
                                    {
                                        batchColsum4 = Math.Abs(int.Parse(batchRow1[4])) - Math.Abs(int.Parse(batchRow2[4]));

                                    }

                                    if (batchRow1.Length == 6)
                                    {
                                        batchColsum5 = Math.Abs(int.Parse(batchRow1[5])) - Math.Abs(int.Parse(batchRow2[5]));

                                    }
                                }

                                for (iR = 0; iR < inputRows.Count; iR++)
                                {

                                    inputRow1 = inputRows[iR];


                                    for (i = 1; i < inputRows.Count; i++)
                                    {
                                        inputRow = inputRows[i];

                                        if (isIRMinus)
                                        {

                                            inputColsum0 = Math.Abs(int.Parse(inputRow[0])) - Math.Abs(int.Parse(inputRow1[0]));

                                            inputColsum1 = Math.Abs(int.Parse(inputRow[1])) - Math.Abs(int.Parse(inputRow1[1]));

                                            if (inputRow.Length >= 3)
                                            {

                                                inputColsum2 = Math.Abs(int.Parse(inputRow[2])) - Math.Abs(int.Parse(inputRow1[2]));
                                            }


                                            if (inputRow.Length >= 4)
                                            {

                                                inputColsum3 = Math.Abs(int.Parse(inputRow[3])) - Math.Abs(int.Parse(inputRow1[3]));
                                            }



                                            if (inputRow.Length >= 5)
                                            {
                                                inputColsum4 = Math.Abs(int.Parse(inputRow[4])) + Math.Abs(int.Parse(inputRow1[4]));
                                            }

                                            // else
                                            if (inputRow.Length == 6)
                                            {
                                                inputColsum5 = Math.Abs(int.Parse(inputRow[5])) + Math.Abs(int.Parse(inputRow1[5]));
                                            }
                                        }

                                        inputColsum0 = int.Parse(inputRow[0]) + int.Parse(inputRow1[0]);

                                        inputColsum1 = int.Parse(inputRow[1]) + int.Parse(inputRow1[1]);

                                        if (inputRow.Length >= 3)
                                        {

                                            inputColsum2 = int.Parse(inputRow[2]) + int.Parse(inputRow1[2]);
                                        }


                                        if (inputRow.Length >= 4)
                                        {

                                            inputColsum3 = int.Parse(inputRow[3]) + int.Parse(inputRow1[3]);
                                        }



                                        if (inputRow.Length >= 5)
                                        {
                                            inputColsum4 = int.Parse(inputRow[4]) + int.Parse(inputRow1[4]);
                                        }

                                        // else
                                        if (inputRow.Length == 6)
                                        {
                                            inputColsum5 = int.Parse(inputRow[5]) + int.Parse(inputRow1[5]);
                                        }

                                        for (int tt = 0; tt < inputRow.Length; tt++)
                                        {
                                            int mcount = 0;



                                            if (isLimit)
                                            {


                                                if (inputColsum0 == batchColsum0)
                                                {

                                                    if (batchRow1[0] != batchRow2[0] && inputRow1[0] == inputRow[0])
                                                    {
                                                        inputRow1.Skip(i);
                                                        inputRow.Skip(iR);

                                                    }
                                                    mcount++;
                                                    inputColsum0 = -1;



                                                    if (inputColsum1 == batchColsum1)
                                                    {

                                                        if (batchRow1[1] != batchRow2[1] && inputRow[1] == inputRow1[1])
                                                        {
                                                            inputRow.Skip(i);
                                                            inputRow1.Skip(iR);
                                                        }
                                                        mcount++;
                                                        inputColsum1 = -1;



                                                        if (inputColsum2 == batchColsum2)
                                                        {
                                                            if (batchRow1[2] != batchRow2[2] && inputRow[2] == inputRow1[2])
                                                            {
                                                                inputRow.Skip(i);
                                                                inputRow1.Skip(iR);

                                                            }
                                                            mcount++;
                                                            inputColsum2 = -1;


                                                            if (inputColsum3 == batchColsum3)
                                                            {
                                                                if (batchRow1[3] != batchRow2[3] && inputRow[3] == inputRow1[3])
                                                                {
                                                                    inputRow.Skip(i);
                                                                    inputRow1.Skip(iR);
                                                                }
                                                                mcount++;
                                                                inputColsum3 = -1;



                                                                if (inputRow.Length >= 5 && inputRow.Length == 5)
                                                                {

                                                                    if (inputColsum4 == batchColsum4)
                                                                    {

                                                                        if (batchRow1[4] != batchRow2[4] && inputRow[4] == inputRow1[4])
                                                                        {
                                                                            inputRow.Skip(i);
                                                                            inputRow1.Skip(iR);

                                                                        }

                                                                        mcount++;
                                                                        inputColsum4 = -1;


                                                                        if (inputRow.Length == 6 && batchRow2.Length == 6)
                                                                        {

                                                                            if (inputColsum5 == batchColsum5)
                                                                            {

                                                                                if (batchRow1[5] != batchRow2[5] && inputRow[5] == inputRow1[5])
                                                                                {
                                                                                    inputRow.Skip(i);
                                                                                    inputRow1.Skip(iR);

                                                                                }

                                                                                mcount++;
                                                                                inputColsum5 = -1;

                                                                            }

                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }

                                                if (absolute)
                                                {
                                                    if (mcount == numMinMatch)
                                                    {
                                                        outputRows.Add(inputRow);
                                                        inputRows.RemoveAt(i);
                                                        i--;
                                                        break;

                                                    }

                                                }


                                                else
                                                {
                                                    if (mcount >= numMinMatch)
                                                    {
                                                        outputRows.Add(inputRow);
                                                        inputRows.RemoveAt(i);
                                                        i--;
                                                        break;

                                                    }

                                                }


                                            }
                                            else
                                            {

                                                if (inputColsum0 == batchColsum0)
                                                {

                                                    if (batchRow1[0] != batchRow2[0] && inputRow1[0] == inputRow[0])
                                                    {
                                                        inputRow1.Skip(i);
                                                        inputRow.Skip(iR);

                                                    }
                                                    mcount++;
                                                    inputColsum0 = -1;

                                                }

                                                if (inputColsum1 == batchColsum1)
                                                {

                                                    if (batchRow1[1] != batchRow2[1] && inputRow[1] == inputRow1[1])
                                                    {
                                                        inputRow.Skip(i);
                                                        inputRow1.Skip(iR);
                                                    }
                                                    mcount++;
                                                    inputColsum1 = -1;

                                                }

                                                if (inputColsum2 == batchColsum2)
                                                {
                                                    if (batchRow1[2] != batchRow2[2] && inputRow[2] == inputRow1[2])
                                                    {
                                                        inputRow.Skip(i);
                                                        inputRow1.Skip(iR);

                                                    }
                                                    mcount++;
                                                    inputColsum2 = -1;

                                                }

                                                if (inputColsum3 == batchColsum3)
                                                {
                                                    if (batchRow1[3] != batchRow2[3] && inputRow[3] == inputRow1[3])
                                                    {
                                                        inputRow.Skip(i);
                                                        inputRow1.Skip(iR);
                                                    }
                                                    mcount++;
                                                    inputColsum3 = -1;

                                                }

                                                if (inputRow.Length >= 5 && batchRow2.Length >= 5)
                                                {

                                                    if (inputColsum4 == batchColsum4)
                                                    {

                                                        if (batchRow1[4] != batchRow2[4] && inputRow[4] == inputRow1[4])
                                                        {
                                                            inputRow.Skip(i);
                                                            inputRow1.Skip(iR);

                                                        }

                                                        mcount++;
                                                        inputColsum4 = -1;

                                                    }
                                                }



                                                if (inputRow.Length == 6 && batchRow2.Length == 6)
                                                {

                                                    if (inputColsum5 == batchColsum5)
                                                    {

                                                        if (batchRow1[5] != batchRow2[5] && inputRow[5] == inputRow1[5])
                                                        {
                                                            inputRow.Skip(i);
                                                            inputRow1.Skip(iR);

                                                        }

                                                        mcount++;
                                                        inputColsum5 = -1;

                                                    }
                                                }


                                            }


                                            if (absolute)
                                            {
                                                if (mcount == numMinMatch)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;

                                                }

                                            }


                                            else
                                            {
                                                if (mcount >= numMinMatch)
                                                {
                                                    outputRows.Add(inputRow);
                                                    inputRows.RemoveAt(i);
                                                    i--;
                                                    break;

                                                }

                                            }
                                        }
                                    }

                                }
                            }
                        }
                    }


                    if (haveIndices)/////////////////////////////////////////////
                    {
                        int batchIndexSum = 0;
                        int inputRowIndexSum = 0;

                        // string[] inputRow = new string[(int)numTargetCols.Value];
                        //string[] inputRow1 = new string[(int)numTargetCols.Value];
                        string[] batchRow = new string[(int)numTargetCols.Value];
                        string[] batchRow1 = new string[(int)numTargetCols.Value];
                        //int i = 0;



                        for (int kk = batchRows.Count - 2; kk > -1; kk--)
                        {

                            // int mcount = 0;
                            batchRow = batchRows[kk];

                            batchRow1 = batchRows[kk + 1];


                            for (i = inputRows.Count - 2; i > -1; i--)
                            {
                                string[] inputRow = inputRows[i];

                                //for (int iR = inputRows.Count - 2; iR > -1; iR--)
                                //{

                                string[] inputRow1 = inputRows[i + 1];



                                for (int k = 0; k < colIndices.Count; k++)
                                {

                                    int mCount = 0;

                                    inputRowIndexSum = int.Parse(inputRow[colIndices[k]]) + int.Parse(inputRow1[colIndices[k]]);
                                    batchIndexSum = int.Parse(batchRow1[colIndices[k]]) + int.Parse(batchRow[colIndices[k]]);


                                    if (inputRowIndexSum == batchIndexSum)
                                    {
                                        if (batchRow1[k] != batchRow[k] && inputRow[k] == inputRow1[k])
                                        {
                                            inputRow.Skip(i);
                                            inputRow1.Skip(i);

                                        }

                                        mCount++;
                                        inputRowIndexSum = -1;
                                        batchIndexSum = -1;
                                        //break;
                                    }



                                    if (absolute)
                                    {
                                        if (mCount == numMinMatch)//Here for absulte equality
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;
                                        }

                                    }

                                    else
                                    {
                                        if (mCount >= numMinMatch)
                                        {
                                            outputRows.Add(inputRow);
                                            inputRows.RemoveAt(i);
                                            i--;
                                            break;

                                        }
                                    }
                                }
                            }
                        }
                    }
                }


                #endregion

                #region ColSums
                if (colSums)
                {
                    string[] powerCheck1 = new string[6];///

                    for (i = 0; i < inputRows.Count; i++)//, m++)
                    {
                        string[] inputRow = inputRows[i];

                        string[] a = inputRow.Take(2).ToArray();
                        int[] firstColSum = Array.ConvertAll(a, int.Parse);
                        inputRowColSum[0] = (firstColSum.Sum());

                        string[] b = inputRow.Take(3).ToArray();
                        int[] secondColSum = Array.ConvertAll(b, int.Parse);
                        inputRowColSum[1] = (secondColSum.Sum());

                        string[] c = inputRow.Take(4).ToArray();
                        int[] thirdColSum = Array.ConvertAll(c, int.Parse);
                        inputRowColSum[2] = (thirdColSum.Sum());

                        string[] d = inputRow.Take(5).ToArray();
                        int[] fourthColSum = Array.ConvertAll(d, int.Parse);
                        inputRowColSum[3] = (fourthColSum.Sum());

                        string[] e = inputRow.Take(6).ToArray();
                        int[] fifthColSum = Array.ConvertAll(e, int.Parse);
                        inputRowColSum[4] = (fifthColSum.Sum());


                        int firstCoupleSum = 0;
                        int secondCoupleSum = 0;
                        int thirdCoupleSum = 0;
                        int fourthCoupleSum = 0;
                        int fifthCoupleSum = 0;
                        int firstInputCol = 0;
                        int secondInputCol = 0;
                        int thirdInputCol = 0;
                        int fourthInputCol = 0;
                        int fifthInputCol = 0;
                        // int fourthInputCoupleSum = 0;
                        int sixthInputCol = 0;
                        //int fifthInputCoupleSum = 0;


                        #region Matching Couple Sums
                        if (powerMatch)
                        {

                            int mCount = 0;

                            if (inputRow.Length == 3)

                            {
                                powerCheck1 = inputRow.Take(3).ToArray();

                                firstInputCol = int.Parse(powerCheck1[0]);
                                secondInputCol = int.Parse(powerCheck1[1]);
                                firstCoupleSum = firstInputCol + secondInputCol;

                                //secondInputCol = int.Parse(powerCheck1[1]);
                                thirdInputCol = int.Parse(powerCheck1[2]);
                                secondCoupleSum = secondInputCol + thirdInputCol;

                            }

                            if (inputRow.Length == 4)

                            {
                                powerCheck1 = inputRow.Take(4).ToArray();

                                firstInputCol = int.Parse(powerCheck1[0]);
                                secondInputCol = int.Parse(powerCheck1[1]);
                                firstCoupleSum = firstInputCol + secondInputCol;

                                //secondInputCol = int.Parse(powerCheck1[1]);
                                thirdInputCol = int.Parse(powerCheck1[2]);
                                secondCoupleSum = secondInputCol + thirdInputCol;

                                //thirdInputCol = int.Parse(powerCheck1[2]);
                                fourthInputCol = int.Parse(powerCheck1[3]);
                                thirdCoupleSum = thirdInputCol + fourthInputCol;

                            }


                            if (inputRow.Length == 5)
                            {
                                powerCheck1 = inputRow.Take(5).ToArray();

                                firstInputCol = int.Parse(powerCheck1[0]);
                                secondInputCol = int.Parse(powerCheck1[1]);
                                firstCoupleSum = firstInputCol + secondInputCol;

                                // secondInputCol = int.Parse(powerCheck1[1]);
                                thirdInputCol = int.Parse(powerCheck1[2]);
                                secondCoupleSum = secondInputCol + thirdInputCol;

                                //thirdInputCol = int.Parse(powerCheck1[2]);
                                fourthInputCol = int.Parse(powerCheck1[3]);
                                thirdCoupleSum = thirdInputCol + fourthInputCol;


                                fifthInputCol = int.Parse(powerCheck1[4]);
                                fourthCoupleSum = fourthInputCol + fifthInputCol;


                            }

                            if (inputRow.Length == 6)
                            {

                                powerCheck1 = inputRow.Take(6).ToArray();

                                firstInputCol = int.Parse(powerCheck1[0]);
                                secondInputCol = int.Parse(powerCheck1[1]);
                                firstCoupleSum = firstInputCol + secondInputCol;


                                thirdInputCol = int.Parse(powerCheck1[2]);
                                secondCoupleSum = secondInputCol + thirdInputCol;


                                fourthInputCol = int.Parse(powerCheck1[3]);
                                thirdCoupleSum = thirdInputCol + fourthInputCol;


                                fifthInputCol = int.Parse(powerCheck1[4]);
                                fourthCoupleSum = fourthInputCol + fifthInputCol;

                                sixthInputCol = int.Parse(powerCheck1[5]);
                                fifthCoupleSum = fifthInputCol + sixthInputCol;
                            }


                            #region MyClamps-Region

                            if (clamped)
                            {

                                if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value
                                    && secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value
                                    || thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value
                                    && thirdCoupleSum >= numPowermin2.Value && thirdCoupleSum <= numPowerMax2.Value
                                    || thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value
                                    && firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value)
                                {

                                    mCount++;
                                }

                            }

                            if (clampedOpt2)
                            {

                                if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value
                                    && secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value)

                                {
                                    mCount++;
                                    // break;
                                }

                                if (thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value
                                   && firstCoupleSum >= numPowermin2.Value && firstCoupleSum <= numPowerMax2.Value)

                                {
                                    mCount++;
                                    //  break;

                                }

                            }
                            else
                            {
                                if (inputRow.Length == 3)
                                {
                                    if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value)
                                    {

                                        mCount++;
                                        firstCoupleSum = -1;
                                    }

                                    if (secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value)
                                    {

                                        mCount++;
                                        secondCoupleSum = -1;

                                    }

                                }

                                #endregion


                                #region MyMCounting-Region
                                if (inputRow.Length == 4)
                                {
                                    if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value)
                                    {

                                        mCount++;
                                        firstCoupleSum = -1;
                                    }

                                    if (secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value)
                                    {

                                        mCount++;
                                        secondCoupleSum = -1;

                                    }
                                    if (thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value)
                                    {
                                        mCount++;
                                        thirdCoupleSum = -1;
                                    }
                                }


                                if (inputRow.Length == 5)
                                {

                                    if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value)
                                    {

                                        mCount++;
                                        firstCoupleSum = -1;
                                    }

                                    if (secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value)
                                    {

                                        mCount++;
                                        secondCoupleSum = -1;

                                    }
                                    if (thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value)
                                    {
                                        mCount++;
                                        thirdCoupleSum = -1;
                                    }
                                    if (fourthCoupleSum >= numPowermin4.Value && fourthCoupleSum <= numPowerMax4.Value)
                                    {
                                        mCount++;
                                        fourthCoupleSum = -1;
                                    }
                                }

                                if (inputRow.Length == 6)
                                {

                                    if (firstCoupleSum >= numPowermin1.Value && firstCoupleSum <= numPowerMax1.Value)
                                    {

                                        mCount++;
                                        firstCoupleSum = -1;
                                    }

                                    if (secondCoupleSum >= numPowermin2.Value && secondCoupleSum <= numPowerMax2.Value)
                                    {

                                        mCount++;
                                        secondCoupleSum = -1;

                                    }
                                    if (thirdCoupleSum >= numPowermin3.Value && thirdCoupleSum <= numPowerMax3.Value)
                                    {
                                        mCount++;
                                        thirdCoupleSum = -1;
                                    }
                                    if (fourthCoupleSum >= numPowermin4.Value && fifthCoupleSum <= numPowerMax4.Value)
                                    {
                                        mCount++;
                                        fourthCoupleSum = -1;
                                    }
                                    if (fifthCoupleSum >= numPowermin5.Value && fifthCoupleSum <= numPowerMax5.Value)
                                    {
                                        mCount++;
                                        fifthCoupleSum = -1;
                                    }


                                }



                                #endregion

                                if (absolute)
                                {
                                    if (mCount == numMinMatch)//Here for absulte equality
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                    }
                                }

                                else
                                {
                                    if (mCount >= numMinMatch)
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;


                                    }
                                    // break;
                                }


                            }
                        }

                        #endregion

                        if (powerOption)
                        {
                            #region PowerOption
                            for (int j = 0; j < numDelinCount.Value; j++)
                            {
                                string[] batchRow = batchRows[j];

                                int colSum = 0;

                                string[] col1 = batchRow.Take(2).ToArray();
                                int[] batchColA = Array.ConvertAll(col1, int.Parse);
                                batchColSum.Add(batchColA.Sum());

                                string[] col3 = batchRow.Take(3).ToArray();
                                int[] batchColB = Array.ConvertAll(col3, int.Parse);
                                batchColSum.Add(batchColB.Sum());

                                string[] col4 = batchRow.Take(4).ToArray();
                                int[] batchColC = Array.ConvertAll(col4, int.Parse);
                                batchColSum.Add(batchColC.Sum());

                                string[] col5 = batchRow.Take(5).ToArray();
                                int[] batchColD = Array.ConvertAll(col5, int.Parse);
                                batchColSum.Add(batchColD.Sum());

                                string[] col6 = batchRow.Take(6).ToArray();
                                int[] batchColE = Array.ConvertAll(col6, int.Parse);
                                batchColSum.Add(batchColE.Sum());



                                if (powerOption)
                                {

                                    int firsColBatch = 0;
                                    int secondColBatch = 0;

                                    int thirdColBatch = 0;
                                    int fourthColBatch = 0;
                                    int fifthColBatch = 0;
                                    int sixthColBatch = 0;
                                    int thirdBatchCoupleSum = 0;
                                    int fourthBatchCoupleSum = 0;
                                    int fifthBatchCoupleSum = 0;


                                    powerCheck1 = inputRow.Take(6).ToArray();

                                    #region BatchingRegion
                                    if (powerBatch)
                                    {
                                        int firstBatchCoupleSum = 0;
                                        int secondBatchCoupleSum = 0;
                                        string[] powerCheck2 = null;

                                        if (inputRow.Length == 3)
                                        {
                                            powerCheck1 = inputRow.Take(3).ToArray();


                                            firstInputCol = int.Parse(powerCheck1[0]);
                                            secondInputCol = int.Parse(powerCheck1[1]);
                                            firstCoupleSum = firstInputCol + secondInputCol;

                                            thirdInputCol = int.Parse(powerCheck1[2]);
                                            secondCoupleSum = secondInputCol + thirdInputCol;

                                            powerCheck2 = batchRow.Take(3).ToArray();
                                            firsColBatch = int.Parse(powerCheck2[0]);
                                            secondColBatch = int.Parse(powerCheck2[1]);

                                            firstBatchCoupleSum = firsColBatch + secondColBatch;

                                            thirdColBatch = int.Parse(powerCheck2[2]);
                                            secondBatchCoupleSum = secondColBatch + thirdColBatch;


                                        }

                                        if (inputRow.Length == 4)

                                        {
                                            powerCheck1 = inputRow.Take(4).ToArray();

                                            firstInputCol = int.Parse(powerCheck1[0]);
                                            secondInputCol = int.Parse(powerCheck1[1]);
                                            firstCoupleSum = firstInputCol + secondInputCol;

                                            thirdInputCol = int.Parse(powerCheck1[2]);
                                            secondCoupleSum = secondInputCol + thirdInputCol;

                                            fourthInputCol = int.Parse(powerCheck1[3]);
                                            thirdCoupleSum = thirdInputCol + fourthInputCol;

                                            powerCheck2 = batchRow.Take(4).ToArray();
                                            firsColBatch = int.Parse(powerCheck2[0]);
                                            secondColBatch = int.Parse(powerCheck2[1]);
                                            firstBatchCoupleSum = firsColBatch + secondColBatch;

                                            thirdColBatch = int.Parse(powerCheck2[2]);
                                            secondBatchCoupleSum = secondColBatch + thirdColBatch;

                                            fourthColBatch = int.Parse(powerCheck2[3]);
                                            thirdBatchCoupleSum = thirdColBatch + fourthColBatch;

                                        }

                                        if (inputRow.Length == 5)

                                        {
                                            powerCheck1 = inputRow.Take(5).ToArray();
                                            firstInputCol = int.Parse(powerCheck1[0]);
                                            secondInputCol = int.Parse(powerCheck1[1]);
                                            firstCoupleSum = firstInputCol + secondInputCol;

                                            thirdInputCol = int.Parse(powerCheck1[2]);
                                            secondCoupleSum = secondInputCol + thirdInputCol;

                                            fourthInputCol = int.Parse(powerCheck1[3]);
                                            thirdCoupleSum = thirdInputCol + fourthInputCol;

                                            fifthInputCol = int.Parse(powerCheck1[4]);
                                            fourthCoupleSum = fourthInputCol + fifthInputCol;

                                            powerCheck2 = batchRow.Take(5).ToArray();
                                            firsColBatch = int.Parse(powerCheck2[0]);
                                            secondColBatch = int.Parse(powerCheck2[1]);
                                            firstBatchCoupleSum = firsColBatch + secondColBatch;

                                            thirdColBatch = int.Parse(powerCheck2[2]);
                                            secondBatchCoupleSum = secondColBatch + thirdColBatch;

                                            fourthColBatch = int.Parse(powerCheck2[3]);
                                            thirdBatchCoupleSum = thirdColBatch + fourthColBatch;

                                            fifthColBatch = int.Parse(powerCheck2[4]);
                                            fourthBatchCoupleSum = fourthColBatch + fifthColBatch;


                                        }

                                        if (inputRow.Length == 6)

                                        {

                                            powerCheck1 = inputRow.Take(6).ToArray();
                                            firstInputCol = int.Parse(powerCheck1[0]);
                                            secondInputCol = int.Parse(powerCheck1[1]);
                                            firstCoupleSum = firstInputCol + secondInputCol;

                                            thirdInputCol = int.Parse(powerCheck1[2]);
                                            secondCoupleSum = secondInputCol + thirdInputCol;

                                            fourthInputCol = int.Parse(powerCheck1[3]);
                                            thirdCoupleSum = thirdInputCol + fourthInputCol;

                                            fifthInputCol = int.Parse(powerCheck1[4]);
                                            fourthCoupleSum = fourthInputCol + fifthInputCol;

                                            sixthInputCol = int.Parse(powerCheck1[5]);
                                            fifthCoupleSum = fifthInputCol + sixthInputCol;

                                            powerCheck2 = batchRow.Take(6).ToArray();
                                            firsColBatch = int.Parse(powerCheck2[0]);
                                            secondColBatch = int.Parse(powerCheck2[1]);
                                            firstBatchCoupleSum = firsColBatch + secondColBatch;

                                            thirdColBatch = int.Parse(powerCheck2[2]);
                                            secondBatchCoupleSum = secondColBatch + thirdColBatch;

                                            fourthColBatch = int.Parse(powerCheck2[3]);
                                            thirdBatchCoupleSum = thirdColBatch + fourthColBatch;

                                            fifthColBatch = int.Parse(powerCheck2[4]);
                                            fourthBatchCoupleSum = fourthColBatch + fifthColBatch;

                                            sixthColBatch = int.Parse(powerCheck2[5]);
                                            fifthBatchCoupleSum = fifthColBatch + sixthColBatch;



                                        }

                                        if (highPowered)
                                        {

                                            colSum = 0;
                                            if (firstCoupleSum == firstBatchCoupleSum)
                                            {
                                                colSum++;
                                                firstCoupleSum = -1;
                                            }

                                            if (secondCoupleSum == secondBatchCoupleSum)
                                            {
                                                colSum++;
                                                secondCoupleSum = -1;
                                            }

                                            if (powerCheck1.Length == 4)
                                            {
                                                if (thirdCoupleSum == thirdBatchCoupleSum)
                                                {

                                                    colSum++;
                                                    thirdCoupleSum = -1;
                                                }
                                            }


                                            if (powerCheck1.Length == 5)
                                            {
                                                if (fourthCoupleSum == fourthBatchCoupleSum)
                                                {

                                                    colSum++;
                                                    fourthCoupleSum = -1;
                                                }
                                            }



                                            if (powerCheck1.Length == 6)
                                            {
                                                if (fifthCoupleSum == fifthBatchCoupleSum)
                                                {

                                                    colSum++;
                                                    fifthCoupleSum = -1;
                                                }

                                            }
                                        }

                                        else
                                        {

                                            colSum = 0;
                                            if (firstCoupleSum == firstBatchCoupleSum)
                                            {
                                                colSum++;
                                                firstCoupleSum = -1;


                                                if (secondCoupleSum == secondBatchCoupleSum)
                                                {
                                                    colSum++;
                                                    secondCoupleSum = -1;

                                                    if (powerCheck1.Length == 4)
                                                    {
                                                        if (thirdCoupleSum == thirdBatchCoupleSum)
                                                        {

                                                            colSum++;
                                                            thirdCoupleSum = -1;

                                                            if (powerCheck1.Length == 5)
                                                            {

                                                                if (fourthCoupleSum == fourthBatchCoupleSum)
                                                                {

                                                                    colSum++;

                                                                    fourthCoupleSum = -1;

                                                                    if (powerCheck1.Length == 6)
                                                                    {


                                                                        if (fifthCoupleSum == fifthBatchCoupleSum)
                                                                        {

                                                                            colSum++;
                                                                            fifthCoupleSum = -1;
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                        }


                                        if (absolute)
                                        {
                                            if (colSum == numMinMatch)//Here for absulte equality
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;

                                            }
                                        }

                                        else
                                        {
                                            if (colSum >= numMinMatch)
                                            {
                                                outputRows.Add(inputRow);
                                                inputRows.RemoveAt(i);
                                                i--;
                                                break;

                                            }
                                        }
                                    }
                                }

                            }
                            #endregion PowerOption

                        }
                        #endregion BatchinRegion


                        #region My HAVe INDECES Region
                        if (haveIndices)/////////////////////////////////////////////
                        {
                            int inputRowIndexSum = 0;
                            int batchIndexSum = 0;

                            foreach (string[] batchRow in batchRows)
                            {
                                string[] br = batchRow.Take(targetNumCols).ToArray();

                                int mCount = 0;

                                for (int k = 0; k < colIndices.Count; k++)
                                {

                                    for (int t = 1; t < colIndices.Count; t++)

                                    {
                                        inputRowIndexSum = int.Parse(inputRow[k]) + int.Parse(inputRow[t]);
                                        batchIndexSum = int.Parse(br[k]) + int.Parse(br[t]);

                                    }


                                    if (inputRowIndexSum == batchIndexSum)
                                    {

                                        mCount++;

                                    }
                                }



                                if (absolute)
                                {
                                    if (mCount == numMinMatch)//Here for absulte equality
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;
                                    }

                                }

                                else
                                {
                                    if (mCount >= numMinMatch)
                                    {
                                        outputRows.Add(inputRow);
                                        inputRows.RemoveAt(i);
                                        i--;
                                        break;

                                    }
                                }
                                //}
                            }
                        }


                        #endregion Indeces
                    }

                    
                }


                #endregion Colsum

                //if (m % 5000 == 0)
                //    this.DoProgress(progressVal * 5000);

            }

            try
            {
                sumLogInfoParmLocker.EnterWriteLock();
                // logInfo.TimeStamp = DateTime.Now;
                logInfo.TimeTook += DateTime.Now.Subtract(now).TotalSeconds;
                logInfo.Matched += outputRows.Count;
                logInfo.Unmatched += inputRows.Count;
            }
            finally
            {
                sumLogInfoParmLocker.ExitWriteLock();
            }

            return sumRetainMatched ? outputRows : inputRows;
        }


        private ReaderWriterLockSlim grpMemFilterParmLocker = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim grpMemLogInfoParmLocker = new ReaderWriterLockSlim();
        private List<string[]> GroupMemberFilter(List<string[]> inputRows, int targetNumCols, dynamic filterParam)
        {
            //this.Invoke(new Action(() =>
            //{
            //    lblStatus.Text = "# Group Member Filter";
            //}));
            DateTime now = DateTime.Now;

            List<int[]> groupCriterias = filterParam.groupCriterias;
            bool grpMemRetainMatch = filterParam.grpMemRetainMatch;
            FilterLogInfo logInfo = filterParam.logInfo;
            List<string[]> outputRows = new List<string[]>();

            int[,] rnges = { { 1, 9 }, { 10, 19 }, { 20, 29 }, { 30, 39 }, { 40, 49 }, { 50, 59 }, { 60, 70 } };
            List<List<int[]>> grpCritsRanges = null;
            try
            {
                grpMemFilterParmLocker.EnterWriteLock();
                grpCritsRanges = filterParam.grpCritsRanges;
                if (grpCritsRanges.Count == 0)
                {
                    for (int i = 0; i < groupCriterias.Count; i++)
                    {
                        List<int[]> grpCrit = new List<int[]>();
                        for (int j = 0; j < groupCriterias[i].Length; j++)
                        {
                            if (groupCriterias[i][j] > 0)
                                grpCrit.Add(new int[] { j, groupCriterias[i][j] });
                        }
                        if (grpCrit.Count > 0)
                            grpCritsRanges.Add(grpCrit);
                    }
                }
            }
            finally
            {
                grpMemFilterParmLocker.ExitWriteLock();
            }

            if (grpCritsRanges.Count == 0)
            {
                return inputRows;
            }

            //int m = 1;
            for (int i = 0; start && i < inputRows.Count; i++)//, m++)
            {
                int[] inputRowGroupedArr = { 0, 0, 0, 0, 0, 0, 0 };
                string[] inputRow = inputRows[i];
                for (int j = 0; j < targetNumCols && j < inputRow.Length; j++)
                {
                    int tmpNum = int.Parse(inputRow[j]);
                    for (int k = 0; k < 7; k++)
                    {
                        if (tmpNum >= rnges[k, 0] && tmpNum <= rnges[k, 1])
                            inputRowGroupedArr[k]++;
                    }
                }

                bool isMatched = true;
                foreach (List<int[]> grpCrit in grpCritsRanges)
                {
                    isMatched = true;
                    foreach (int[] grpCritCol in grpCrit)
                    {
                        if (inputRowGroupedArr[grpCritCol[0]] != grpCritCol[1])
                        {
                            isMatched = false;
                            break;
                        }
                    }
                    if (isMatched)
                        break;
                }

                if (isMatched)
                {
                    outputRows.Add(inputRow);
                    inputRows.RemoveAt(i);
                    i--;
                }

                //if (m % 5000 == 0)
                //    this.DoProgress(progressVal * 5000);
            }

            //if (!start)
            //    return;

            //m--;
            //if (m % 5000 != 0)
            //    this.DoProgress(progressVal * (m % 5000));

            try
            {
                grpMemLogInfoParmLocker.EnterWriteLock();
                //logInfo.TimeStamp = DateTime.Now;
                logInfo.TimeTook += DateTime.Now.Subtract(now).TotalSeconds;
                logInfo.Matched += outputRows.Count;
                logInfo.Unmatched += inputRows.Count;
            }
            finally
            {
                grpMemLogInfoParmLocker.ExitWriteLock();
            }

            return grpMemRetainMatch ? outputRows : inputRows;
        }

        private ReaderWriterLockSlim grpRCFilterParmLocker = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim grpRCLogInfoParmLocker = new ReaderWriterLockSlim();
        private List<string[]> GroupRowColumnFilter(List<string[]> inputRows, int targetNumCols, dynamic filterParam)
        {
            //this.Invoke(new Action(() =>
            //{
            //    lblStatus.Text = "# Group Row Column Filter";
            //}));
            DateTime now = DateTime.Now;

            List<string[]> groupRowCols = filterParam.groupRowCols;
            int grpRwClMinMatch = filterParam.grpRwClMinMatch;
            bool grpRwClRetainMatch = filterParam.grpRwClRetainMatch;
            FilterLogInfo logInfo = filterParam.logInfo;
            List<string[]> outputRows = new List<string[]>();

            List<int> grpClmIndices = null;
            List<List<string>> grpRowVals = null;
            try
            {
                grpRCFilterParmLocker.EnterWriteLock();
                grpClmIndices = filterParam.grpClmIndices;
                grpRowVals = filterParam.grpRowVals;
                if (grpClmIndices.Count == 0)
                {
                    for (int i = 0; i < targetNumCols; i++)
                    {
                        List<string> rowVals = new List<string>();
                        foreach (string rw in groupRowCols[i])
                        {
                            if (rw.Length == 2)
                                rowVals.Add(rw);
                        }
                        if (rowVals.Count > 0)
                        {
                            grpClmIndices.Add(i);
                            grpRowVals.Add(rowVals);
                        }
                    }
                }
            }
            finally
            {
                grpRCFilterParmLocker.ExitWriteLock();
            }

            if (grpClmIndices.Count == 0)
            {
                return inputRows;
            }

            //int m = 1;
            for (int i = 0; start && i < inputRows.Count; i++)//, m++)
            {
                string[] inputRow = inputRows[i];
                int mCount = 0;
                for (int j = 0; j < grpClmIndices.Count; j++)
                {
                    if (grpRowVals[j].Contains(inputRow[grpClmIndices[j]]))
                        mCount++;
                }

                if (mCount >= grpRwClMinMatch || mCount == grpClmIndices.Count)
                {
                    outputRows.Add(inputRow);
                    inputRows.RemoveAt(i);
                    i--;
                }

                //if (m % 5000 == 0)
                //    this.DoProgress(progressVal * 5000);
            }

            //m--;
            //if (m % 5000 != 0)
            //    this.DoProgress(progressVal * (m % 5000));

            try
            {
                grpRCLogInfoParmLocker.EnterWriteLock();
                //logInfo.TimeStamp = DateTime.Now;
                logInfo.TimeTook += DateTime.Now.Subtract(now).TotalSeconds;
                logInfo.Matched += outputRows.Count;
                logInfo.Unmatched += inputRows.Count;
            }
            finally
            {
                grpRCLogInfoParmLocker.ExitWriteLock();
            }

            return grpRwClRetainMatch ? outputRows : inputRows;
        }

        private byte[] inputFileBuffer = new byte[500 * 1024];
        private ReaderWriterLockSlim inputFileLocker = new ReaderWriterLockSlim();
        private StringBuilder ReadInputFile(FileStream fs)
        {
            StringBuilder builder = new StringBuilder();
            try
            {
                inputFileLocker.EnterWriteLock();
                int count = fs.Read(inputFileBuffer, 0, inputFileBuffer.Length);
                if (count > 0)
                {
                    builder.Append(Encoding.ASCII.GetChars(inputFileBuffer, 0, count));
                    int singleByte = -1;
                    while ((singleByte = fs.ReadByte()) != -1)
                    {
                        if (singleByte == '\n')
                            break;
                        builder.Append((char)singleByte);
                    }
                }
            }
            finally
            {
                inputFileLocker.ExitWriteLock();
            }

            return builder;
        }

        private ReaderWriterLockSlim writeFilterLocker = new ReaderWriterLockSlim();
        private void WriteFilterOutput(FileStream fs, List<string[]> rows)
        {
            try
            {
                writeFilterLocker.EnterWriteLock();

                StringBuilder bob = new StringBuilder();
                foreach (string[] row in rows)
                {
                    foreach (string col in row)
                    {
                        bob.Append(col);
                        bob.Append(" ");
                    }
                    bob.Remove(bob.Length - 1, 1);
                    bob.Append("\r\n");
                }
                char[] chars = new char[bob.Length];
                bob.CopyTo(0, chars, 0, bob.Length);
                bob.Clear();
                byte[] buffer = Encoding.ASCII.GetBytes(chars);
                chars = null;
                fs.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                writeFilterLocker.ExitWriteLock();
            }
        }

        private class FilterLogInfo
        {
            //public DateTime TimeStamp = DateTime.Now;
            public double TimeTook = 0;
            public int Matched = 0;
            public int Unmatched = 0;
        }

        private double progressSum = 10;
        private ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
        private void DoProgress(double val)
        {
            try
            {
                locker.EnterWriteLock();
                progressSum += val;
                this.Invoke(new Action(() =>
                {
                    if (progress.Value < (int)progressSum)
                        progress.Value = (int)progressSum;
                }));
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        private void GenerateGRCRtxtboxes(int num)
        {
            //int horSpace = pnlGroupRowColFilter.Controls["txtGrpRowColB0"].Left -
            //               pnlGroupRowColFilter.Controls["txtGrpRowColA0"].Right;
            int verSpace = pnlGroupRowColFilter.Controls["txtGrpRowColA1"].Top -
                           pnlGroupRowColFilter.Controls["txtGrpRowColA0"].Bottom;
            int boxWidth = pnlGroupRowColFilter.Controls["txtGrpRowColA0"].Width;
            int boxHeight = pnlGroupRowColFilter.Controls["txtGrpRowColA0"].Height;
            int boxTop = pnlGroupRowColFilter.Controls["txtGrpRowColA0"].Top;
            for (int i = 0; i < 6; i++)
            {
                char colChar = (char)('A' + i);
                int boxLeft = pnlGroupRowColFilter.Controls["txtGrpRowCol" + colChar + "0"].Left;
                for (int j = 0; j < num; j++)
                {
                    TextBox txt = new TextBox();
                    txt.Name = "txtGrpRowCol" + colChar + (NUM_OF_GRPROWCOL + j);
                    txt.Width = boxWidth;
                    txt.Height = boxHeight;
                    txt.Left = boxLeft;
                    txt.Top = boxTop + ((NUM_OF_GRPROWCOL + j) * (boxHeight + verSpace));
                    txt.TextAlign = HorizontalAlignment.Center;
                    txt.KeyDown += this.textBox_KeyDown;
                    pnlGroupRowColFilter.Controls.Add(txt);
                }
            }
            NUM_OF_GRPROWCOL += num;
            for (int i = 0, k = 1; i < 6; i++)
            {
                for (int j = 0; j < NUM_OF_GRPROWCOL; j++)
                    pnlGroupRowColFilter.Controls["txtGrpRowCol" + (char)('A' + i) + j].TabIndex = k++;
            }
        }

        private List<string[]> ReadRowColumnsFromText(string totalStr)
        {
            return ReadRowColumnsFromText(new StringBuilder(totalStr));
        }

        private List<string[]> ReadRowColumnsFromText(StringBuilder totalStr)
        {
            var tmpRows = new List<string[]>();
            List<string> nums = new List<string>();
            int i = 0, startI = 0;
            for (; start && i < totalStr.Length; i++)
            {
                char ch = totalStr[i];
                if (ch == '\r' || ch == '\n')
                {
                    if (i > startI)
                        nums.Add(totalStr.ToString(startI, i - startI));
                    if (nums.Count > 0)
                    {
                        tmpRows.Add(nums.ToArray());
                        nums.Clear();
                    }
                    while (i < totalStr.Length && (totalStr[i] == '\r' || totalStr[i] == '\n' || totalStr[i] == '\t' || totalStr[i] == ' '))
                        i++;
                    startI = i;
                    i--;
                }
                else if (ch == ' ' || ch == '\t')
                {
                    if (i > startI)
                        nums.Add(totalStr.ToString(startI, i - startI));
                    while (i < totalStr.Length && (totalStr[i] == '\t' || totalStr[i] == ' '))
                        i++;
                    startI = i;
                    i--;
                }
            }
            if (i > startI)
            {
                nums.Add(totalStr.ToString(startI, i - startI));
                tmpRows.Add(nums.ToArray());
                nums.Clear();
            }

            return tmpRows;
        }

        private void SaveSettings()
        {
            StringBuilder bob = new StringBuilder();
            var controls = this.GetAllControls(this);

            foreach (Control cc in controls)
            {
                if (cc is TextBox)
                {
                    bob.Append(cc.Name);
                    bob.Append("@##{}##@");
                    bob.Append(cc.Text);
                    bob.Append("@##[]##@");
                }
                else if (cc is CheckBox)
                {
                    bob.Append(cc.Name);
                    bob.Append("@##{}##@");
                    bob.Append(((CheckBox)cc).Checked);
                    bob.Append("@##[]##@");
                }
                else if (cc is RadioButton)
                {
                    bob.Append(cc.Name);
                    bob.Append("@##{}##@");
                    bob.Append(((RadioButton)cc).Checked);
                    bob.Append("@##[]##@");
                }
                else if (cc is NumericUpDown)
                {
                    bob.Append(cc.Name);
                    bob.Append("@##{}##@");
                    bob.Append(((NumericUpDown)cc).Value);
                    bob.Append("@##[]##@");
                }
                else if (cc is Label)
                {
                    if (cc.Name.StartsWith("lblComb"))
                    {
                        bob.Append(cc.Name);
                        bob.Append("@##{}##@");
                        bob.Append(combDic[(Label)cc].ToString());
                        bob.Append("@##[]##@");
                    }
                    else if (cc.Name.StartsWith("lblAppend"))
                    {
                        bob.Append(cc.Name);
                        bob.Append("@##{}##@");
                        bob.Append(appendDic[(Label)cc].ToString());
                        bob.Append("@##[]##@");
                    }
                }
            }

            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            StreamWriter writer = new StreamWriter(appDataPath + "\\settings.txt", false);
            writer.Write(bob.ToString());
            writer.Flush();
            writer.Close();
            writer.Dispose();
        }

        private void LoadSettings()
        {
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            if (!File.Exists(appDataPath + "\\settings.txt"))
                return;

            StreamReader reader = new StreamReader(appDataPath + "\\settings.txt");
            string settingsTxt = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            string[] rows = settingsTxt.Split(new string[] { "@##[]##@" }, StringSplitOptions.RemoveEmptyEntries);
            //List<string[]> rowCol = new List<string[]>();
            Dictionary<string, string> rowCol = new Dictionary<string, string>();
            foreach (string row in rows)
            {
                string[] split = row.Split(new string[] { "@##{}##@" }, StringSplitOptions.None);
                rowCol.Add(split[0], split[1]);

            }

            List<Control> controls = this.GetAllControls(this);

            foreach (Control cc in controls)
            {
                try
                {
                    if (cc is TextBox)
                    {
                        TextBox txt = cc as TextBox;
                        txt.Text = rowCol[cc.Name];
                        if (txt.Multiline == false)
                            txt.Select(txt.Text.Length, 0);
                    }
                    else if (cc is CheckBox)
                        ((CheckBox)cc).Checked = bool.Parse(rowCol[cc.Name]);
                    else if (cc is RadioButton)
                        ((RadioButton)cc).Checked = bool.Parse(rowCol[cc.Name]);
                    else if (cc is NumericUpDown)
                        ((NumericUpDown)cc).Value = int.Parse(rowCol[cc.Name]);
                    else if (cc is Label)
                    {
                        if (cc.Name.StartsWith("lblComb") && bool.Parse(rowCol[cc.Name]))
                            this.lblComb_Click(cc, null);
                        else if (cc.Name.StartsWith("lblAppend") && bool.Parse(rowCol[cc.Name]))
                            this.lblAppend_Click(cc, null);
                    }
                }
                catch { }
            }
        }

        private List<Control> GetAllControls(Control parent)
        {
            List<Control> controls = new List<Control>();
            List<Control> stack = new List<Control>();
            stack.Add(this);
            while (stack.Count > 0)
            {
                Control c = stack[0];
                stack.RemoveAt(0);
                foreach (Control cc in c.Controls)
                {
                    if (cc is TabControl)
                    {
                        foreach (TabPage tp in ((TabControl)cc).TabPages)
                            stack.Add(tp);
                    }
                    else if (cc is GroupBox || cc is Panel)
                        stack.Add(cc);
                    else
                        controls.Add(cc);
                }
            }
            return controls;
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Back && e.KeyCode != Keys.Delete)
            {
                TextBox txt = sender as TextBox;
                if (txt.Text.Length > 1 || !(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9) && !(e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9))
                    e.SuppressKeyPress = true;
            }
        }

        private void btnBrowseInput_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.RestoreDirectory = true;
            ofd.Multiselect = false;
            ofd.CheckFileExists = false;
            ofd.CheckPathExists = false;
            ofd.Title = "Please choose input file path :";
            ofd.Filter = "Input File|*.txt";
            DialogResult result = ofd.ShowDialog();
            if (result == DialogResult.OK)
            {
                txtBrowseInput.Text = ofd.FileName;
                txtBrowseInput.Select(txtBrowseInput.Text.Length, 0);
            }
        }

        private void menuClear_Click(object sender, EventArgs e)
        {
            var controls = this.GetAllControls(this);
            foreach (Control gc in controls)
            {
                if (gc is TextBox)
                    gc.Text = "";
                if (gc is NumericUpDown)
                    ((NumericUpDown)gc).Value = ((NumericUpDown)gc).Maximum;
                if (gc is CheckBox)
                    ((CheckBox)gc).Checked = false;
            }
            chkBatchScanFilter.Checked = true;
            chkGroupMemberFilter.Checked = true;
            chkGrpRowColFilter.Checked = true;
            rdoBatchMatch.Checked = true;
            rdoGrpMemMatch.Checked = true;
            rdoGrpRwClMatch.Checked = true;
            lblStatus.Text = "";
            progress.Value = progress.Minimum;
            btnBrowseInput.Focus();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            menuClear_Click(null, null);
            LoadSettings();

            List<string[]> lstItems = new List<string[]>
            {
                new string[] { "01", "5++", "0", "0", "0" },
                new string[] { "02", "6", "0", "0", "0" },
                new string[] { "03", "5+", "0", "0", "0" },
                new string[] { "04", "5", "0", "0", "0" },
                new string[] { "05", "4++", "0", "0", "0" },
                new string[] { "06", "4+", "0", "0", "0" },
                new string[] { "07", "4", "0", "0", "0" },
                new string[] { "08", "2++", "0", "0", "0" },
                new string[] { "09", "3+", "0", "0", "0" },
                new string[] { "10", "3", "0", "0", "0" },
                new string[] { "11", "1++", "0", "0", "0" },
                new string[] { "12", "2+", "0", "0", "0" },
                new string[] { "13", "2", "0", "0", "0" },
                new string[] { "14", "1+", "0", "0", "0" },
                new string[] { "15", "+", "0", "0", "0" },
            };
            foreach (var lstItm in lstItems)
            {
                ListViewItem item = new ListViewItem(lstItm);
                //item.BackColor = "";
                this.listView1.Items.Add(item);
            }
            txtCounterTotal.Text = "0.00";
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (start)
            {
                MessageBox.Show("Number Processor is working. Please Stop it first then Close.");
                e.Cancel = true;
                return;
            }
            SaveSettings();
        }

        public FrmMain()
        {
            InitializeComponent();
            this.GenerateGRCRtxtboxes(70);
            this.PutGenLbl2Dic();
        }

        private Dictionary<Label, bool> combDic = new Dictionary<Label, bool>();
        private Dictionary<Label, bool> appendDic = new Dictionary<Label, bool>();
        private List<Label> combLblList = new List<Label>();
        private void PutGenLbl2Dic()
        {
            foreach (Control c in grpGenerator.Controls)
            {
                if (c is Label)
                {
                    if (c.Name.StartsWith("lblComb"))
                    {
                        combDic.Add(c as Label, false);
                        combLblList.Add(c as Label);
                    }
                    else if (c.Name.StartsWith("lblAppend"))////
                        appendDic.Add(c as Label, false);
                }
            }
            combLblList.Sort((a, b) => string.Compare(a.Text, b.Text));
        }

        private Color normalColor = Color.FromArgb(240, 240, 240);
        private Color selectColor = Color.FromArgb(113, 184, 255);
        private Color hoverColor = Color.FromArgb(248, 248, 248);
        private void lblComb_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            combDic[lbl] = !state;
        }

        private void lblAppend_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = appendDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            appendDic[lbl] = !state;
        }

        private void lblComb_MouseEnter(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
                lbl.BackColor = hoverColor;
        }

        private void lblComb_MouseLeave(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
                lbl.BackColor = normalColor;
        }

        private void lblAppend_MouseEnter(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = appendDic[lbl];
            if (!state)
                lbl.BackColor = hoverColor;
        }

        private void lblAppend_MouseLeave(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = appendDic[lbl];
            if (!state)
                lbl.BackColor = normalColor;
        }

        private void chkAppA_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAppA.Checked && chkAppB.Checked)
                chkAppB.Checked = false;
        }

        private void chkAppB_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAppB.Checked && chkAppA.Checked)
                chkAppA.Checked = false;
        }

        private void rdoCombTypeA_CheckedChanged(object sender, EventArgs e)
        {
            bool isCombA = rdoCombTypeA.Checked || rdoCombTypeA1.Checked || rdoCombTypeA2.Checked || rdoCombTypeA3.Checked;
            for (int i = 70; i < combLblList.Count; i++)
            {
                Label lbl = combLblList[i];
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
                lbl.Enabled = isCombA;
                combDic[lbl] = false;
            }

        }

        private void linkCombReset_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            foreach (Label lbl in combLblList)
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
                combDic[lbl] = false;
            }
        }

        private void linkAppendReset_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            List<Label> lbls = appendDic.Keys.ToList();
            foreach (Label lbl in lbls)
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
                appendDic[lbl] = false;
            }
        }

        private void btnGenStart_Click(object sender, EventArgs e)
        {
            if (!start)
            {
                bool isCombTypeA = rdoCombTypeA.Checked;
                bool isCombTypeA1 = rdoCombTypeA1.Checked;
                bool isCombTypeA2 = rdoCombTypeA2.Checked;
                bool isCombTypeA3 = rdoCombTypeA3.Checked;
                bool isCombTypeB = rdoCombTypeB.Checked;
                bool isCombTypeB1 = rdoCombTypeB1.Checked;
                bool isCombTypeB2 = rdoCombTypeB2.Checked;
                bool isCombTypeB3 = rdoCombTypeB3.Checked;

                int objsLen = isCombTypeA || isCombTypeA1 || isCombTypeA2 || isCombTypeA3 ? 90 : 50;

                int colLen = 0;
                if (isCombTypeA)
                    colLen = 6;
                else if (isCombTypeA1 || isCombTypeB)
                    colLen = 5;
                else if (isCombTypeA2 || isCombTypeB1)
                    colLen = 4;
                else if (isCombTypeA3 || isCombTypeB2)
                    colLen = 3;
                else if (isCombTypeB3)
                    colLen = 2;

                List<string> objs = new List<string>();
                for (int i = 0; i < objsLen; i++)
                    if (combDic[combLblList[i]])
                        objs.Add(combLblList[i].Text);
                if (objs.Count == 0)
                    for (int i = 0; i < objsLen; i++)
                        objs.Add(combLblList[i].Text);

                //if (isCombTypeA || isCombTypeA1 || isCombTypeA2 || isCombTypeA3)
                //    chkAppA.Checked = false;
                //    chkAppB.Checked = false;


                #region Validation
                if (objs.Count < colLen)
                {
                    MessageBox.Show("You Must Select atleast " + colLen + " Objects for \"Comb Type " + (isCombTypeA ? "A" : "B") + "\"");
                    return;
                }
                #endregion Validation
                bool isAppend = !isCombTypeA && chkAppA.Checked || chkAppB.Checked;
                bool isAppendTypeA = chkAppA.Checked;
                bool isAppendInter = chkAppInter.Checked;



                List<string> objsAppend = new List<string>();
                if (isAppend)
                {
                    foreach (Label c in appendDic.Keys)
                        if (appendDic[c])
                            objsAppend.Add(c.Text);
                    if (objsAppend.Count == 0)
                        foreach (Label c in appendDic.Keys)
                            objsAppend.Add(c.Text);
                    objsAppend.Sort();
                }
                if (isAppendInter)
                {
                    foreach (Label c in appendDic.Keys)
                        if (appendDic[c])
                            objsAppend.Add(c.Text);
                    if (objsAppend.Count == 0)
                        foreach (Label c in appendDic.Keys)
                            objsAppend.Add(c.Text);
                    objsAppend.Sort();
                }

                progressSum = 0;
                progress.Maximum = 100;
                progress.Value = 0;
                btnStartBatchFilter.Enabled = false;
                btnCountingStart.Enabled = false;
                btnGenStart.Text = "Stop";
                start = true;

                new Thread(() =>
                {
                    #region Thread

                    DateTime now = DateTime.Now;
                    objsLen = objs.Count;
                    int totalLines = (int)(this.Factorial(objsLen, colLen) / this.Factorial(colLen));
                    int thrdLines = totalLines / NUM_OF_THREADS;
                    int remLines = totalLines % NUM_OF_THREADS;
                    List<int[]> linesNindex = new List<int[]>();

                    for (int i = 0, j = 0; i < NUM_OF_THREADS; i++, j += thrdLines)
                        linesNindex.Add(new int[] { j, thrdLines, 0, 0 });//start, lenght, obj, objIndex
                    linesNindex[linesNindex.Count - 1][1] += remLines;
                    this.GenerateCombinationIndex(objsLen, colLen, linesNindex);

                    string tmpFileName = Path.Combine(appDataPath, "tmpGenFile" + DateTime.Now.Millisecond + ".txt");
                    FileStream fs = new FileStream(tmpFileName, FileMode.Create, FileAccess.Write, FileShare.None);
                    long totalFileLength = this.GetRowsByteLenght(colLen, totalLines, isAppend, isAppendTypeA, objsAppend.Count);
                    fs.SetLength(totalFileLength);

                    List<Thread> threadList = new List<Thread>();
                    for (int i = 0; i < NUM_OF_THREADS; i++)
                    {
                        Thread t = new Thread(GenerateCombinationThreadMethod);
                        t.Name = i.ToString();
                        t.Start(new { colLen, objs, objsAppend, isAppendTypeA, lineNindex = linesNindex[i], stream = fs, totalFileLength });
                        threadList.Add(t);
                    }
                    foreach (Thread t in threadList)
                        t.Join();
                    //fs.Position = 0;// ((colLen * 3) - 1 + 2) * (long)thrdLines - 5;
                    //byte[] abcd = new byte[10];
                    //fs.Read(abcd, 0, 10);
                    fs.Flush();
                    fs.Close();
                    fs.Dispose();

                    bool tmpStart = start;
                    start = false;
                    this.Invoke((MethodInvoker)delegate
                    {
                        progress.Value = progress.Maximum;
                        btnStartBatchFilter.Enabled = true;
                        btnCountingStart.Enabled = true;
                        btnGenStart.Enabled = true;
                        btnGenStart.Text = "Start";
                    });

                    if (!tmpStart)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblStatus.Text = "# Stopped by User!";
                        }));
                    }
                    else
                    {
                        #region Saving File
                        this.Invoke(new Action(() =>
                        {
                            DialogResult ans = DialogResult.No;
                            ans = MessageBox.Show(this, "Total time took to Generate : " + DateTime.Now.Subtract(now).TotalSeconds.ToString("0.0") + "sec\r\n" +
                                                        "Do you want to Save the file ?", "Number Processor", MessageBoxButtons.YesNo);
                            if (ans == DialogResult.Yes)
                            {
                                SaveFileDialog s = new SaveFileDialog();
                                s.RestoreDirectory = true;
                                s.CheckFileExists = false;
                                s.CheckPathExists = false;
                                s.CreatePrompt = false;
                                s.OverwritePrompt = true;
                                s.Title = "Please choose a filepath to save Generated result.";
                                s.Filter = "Text File|*.txt";
                                s.FileName = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss") + ".txt";
                                ans = s.ShowDialog();
                                if (ans == DialogResult.OK)
                                {
                                    try
                                    {
                                        if (File.Exists(s.FileName))
                                            File.Delete(s.FileName);
                                        File.Move(tmpFileName, s.FileName);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(this, ex.Message, "Number Processor");
                                        File.Delete(tmpFileName);
                                    }
                                }
                                else
                                    File.Delete(tmpFileName);
                            }
                            else
                                File.Delete(tmpFileName);
                        }));
                        #endregion Saving File
                    }

                    #endregion Thread
                }).Start();

                #region Comment
                /*DateTime now = DateTime.Now;
            List<string> objs = new List<string>();
            for (int i = 1; i <= 52; i++)
                objs.Add(i.ToString("00"));
            StringBuilder result = new StringBuilder();
            int[] objsPointers = { 0, 1, 2, 3, 4, 5};
            for (int i = 0; i < 6; i++)
            {
                if (i > 0)
                {
                    objsPointers[5 - i]++;
                    if (objsPointers[5 - i] < objs.Count - i)//objs.Count - 1 - i
                    {
                        for (int j = 5 - i + 1, k = 1; j < objsPointers.Length; j++, k++)
                            objsPointers[j] = objsPointers[5 - i] + k;
                        i = -1;
                    }
                }
                else
                {
                    for (int j = objsPointers[5 - i]; j < objs.Count; j++)
                    {
                        foreach (int p in objsPointers)
                        {
                            result.Append(objs[p]);
                            result.Append(" ");
                        }
                        result.Remove(result.Length - 1, 1);
                        result.Append("\r\n");
                        objsPointers[5 - i]++;
                    }
                }
            }
            result.Remove(result.Length - 2, 2);
            //MessageBox.Show(DateTime.Now.Subtract(now).TotalMilliseconds.ToString("0.0"));
            now = DateTime.Now;

            StreamWriter writer = new StreamWriter("aaaaa.txt");
            int mb = 1024 * 1024;
            int lenRem = result.Length % mb;
            for (int i = 0; i < result.Length; i += mb)
            {
                try
                {
                    writer.Write(result.ToString(i, mb));
                }
                catch
                {
                    writer.Write(result.ToString(i, lenRem));
                }
            }
            writer.Flush();
            writer.Close();
            writer.Dispose();
            MessageBox.Show(DateTime.Now.Subtract(now).TotalMilliseconds.ToString("0.0"));*/
                #endregion Comment
            }
            else
            {
                btnGenStart.Enabled = false;
                start = false;
            }
        }

        private void GenerateCombinationThreadMethod(object tobj)
        {
            //if (Thread.CurrentThread.Name != "29")
            //    return;

            int colLen = ((dynamic)tobj).colLen;
            List<string> objs = ((dynamic)tobj).objs;
            int objLen = objs.Count;
            List<string> objsAppend = ((dynamic)tobj).objsAppend;
            bool isAppend = objsAppend.Count > 0;
            bool isAppendTypeA = ((dynamic)tobj).isAppendTypeA;
            // bool isAppendInter = ((dynamic)tobj).isAppendInter;
            int[] lineNindex = ((dynamic)tobj).lineNindex;
            int lineRowStartIndex = lineNindex[0];
            int linesRowLenght = lineNindex[1];
            int startObjArrIndex = lineNindex[2];
            int startObjRowIndex = lineNindex[3]; //startObjArrIndex = 39; startObjRowIndex = 5;
            FileStream stream = ((dynamic)tobj).stream;
            long totalFileLength = ((dynamic)tobj).totalFileLength;
            int mb = 2 * 1024 * 1024;

            int[] objsPointers = new int[colLen]; //{ 0, 1, 2, 3, 4, 5 };
            objsPointers[0] = startObjArrIndex;
            for (int i = 1, startRowIndex = startObjRowIndex + 1, subObjLen = objLen - 1 - startObjArrIndex; i < colLen; i++)
            {
                //int cmb = (int)(this.Factorial(objLen - i, colLen - i) / this.Factorial(colLen - i));
                var ret = GenerateCombinationIndex(subObjLen, colLen - i, startRowIndex);
                if (ret == null && i == 1 && subObjLen > colLen - 1)
                {
                    objsPointers[0]++;
                    startRowIndex = 1;
                    subObjLen--;
                    i--;
                    continue;
                }
                objsPointers[i] = objsPointers[i - 1] + ret[0] + 1;
                startRowIndex = ret[1];
                subObjLen = ret[2];
            }

            StringBuilder result = new StringBuilder();
            //long filePos = ((colLen * 3) - 1 + 2) * (long)lineRowStartIndex;
            long filePos = this.GetRowsByteLenght(colLen, lineRowStartIndex, isAppend, isAppendTypeA, objsAppend.Count);
            int appCnt = 0;
            for (int i = 0, x = 0, indxLast = colLen - 1; i < colLen && x < linesRowLenght; i++)
            {
                if (i > 0)
                {
                    objsPointers[indxLast - i]++;
                    if (objsPointers[indxLast - i] < objs.Count - i)
                    {
                        for (int j = indxLast - i + 1, k = 1; j < objsPointers.Length; j++, k++)
                            objsPointers[j] = objsPointers[indxLast - i] + k;
                        i = -1;
                    }
                }
                else
                {
                    for (int j = objsPointers[indxLast - i]; j < objs.Count && x < linesRowLenght; j++, x++)
                    {
                        if (!isAppend)
                        {
                            foreach (int p in objsPointers)
                            {
                                result.Append(objs[p]);
                                result.Append(" ");
                            }
                            result.Remove(result.Length - 1, 1);
                            result.Append("\r\n");
                        }
                        else
                        {
                            if (isAppendTypeA)
                            {
                                foreach (int p in objsPointers)
                                {
                                    result.Append(objs[p]);
                                    result.Append(" ");
                                }
                                result.Append(objsAppend[appCnt++]);
                                if (appCnt >= objsAppend.Count)
                                    appCnt = 0;
                                result.Append("\r\n");
                            }
                            else
                            {
                                //if (isAppendInter)
                                //{
                                //    foreach (string appStr in objsAppend)
                                //    {
                                //        foreach (int p in objsPointers)
                                //        {
                                //            result.Append(objs[p]);
                                //            result.Append("  ");
                                //        }
                                //        result.Append(appStr);
                                //        result.Append("\r\n");
                                //    }
                                //}
                                //else
                                //{

                                foreach (string appStr in objsAppend)
                                {
                                    foreach (int p in objsPointers)
                                    {
                                        result.Append(objs[p]);
                                        result.Append(" ");
                                    }
                                    result.Append(appStr);
                                    result.Append("\r\n");
                                }

                                //}
                            }
                        }
                        objsPointers[indxLast - i]++;
                        if (result.Length >= mb)
                        {
                            int tmpLen = result.Length;
                            this.Write(stream, filePos, result);
                            this.DoProgress((tmpLen * 100) / (double)totalFileLength);
                            //filePos = ((colLen * 3) - 1 + 2) * (long)(lineRowStartIndex + x + 1);
                            filePos = this.GetRowsByteLenght(colLen, lineRowStartIndex + x + 1, isAppend, isAppendTypeA, objsAppend.Count);
                            // filePos = this.GetRowsByteLenght(colLen, lineRowStartIndex + x + 2, isAppend, isAppendInter, objsAppend.Count);
                        }
                    }
                }
            }

            if (result.Length > 0)
            {
                int tmpLen = result.Length;
                this.Write(stream, filePos, result);
                this.DoProgress((tmpLen * 100) / (double)totalFileLength);
            }
        }

        private void GenerateCombinationIndex(int total, int reserve, List<int[]> linesNindex)
        {
            int sum = 0;
            int divFact = (int)this.Factorial(reserve - 1);
            for (int i = 0, j = 0; i <= total - reserve && j < linesNindex.Count; i++)
            {
                int rest = total - i - 1;
                int comb = (int)(this.Factorial(rest, reserve - 1) / divFact);
                sum += comb;
                while (j < linesNindex.Count && sum >= (linesNindex[j][0] + 1))
                {
                    linesNindex[j][2] = i;//i + 1
                    linesNindex[j][3] = linesNindex[j][0] - (sum - comb);
                    j++;
                }
            }
        }

        private int[] GenerateCombinationIndex(int total, int reserve, int targetRowIndex)
        {
            int[] ret = null;
            int sum = 0;
            int divFact = (int)this.Factorial(reserve - 1);
            bool isAppendInter = chkAppInter.Checked;
            for (int i = 0; i <= total - reserve; i++)
            {
                if (!isAppendInter)
                {
                    int rest = total - i - 1;
                    int comb = (int)(this.Factorial(rest, reserve - 1) / divFact);
                    sum += comb;
                    if (sum >= targetRowIndex)
                    {
                        ret = new int[3];
                        ret[0] = i;
                        ret[1] = targetRowIndex - (sum - comb);
                        ret[2] = rest;
                        break;
                    }
                }
            }

            return ret;
        }

        private long Factorial(int num, int limit)
        {
            long ret = 1;
            for (int i = 0; i < limit; i++)
                ret *= (num - i);
            return ret;
        }

        private long Factorial(int num)
        {
            bool isAppendInter = chkAppInter.Checked;
            if (isAppendInter)
            {
                int limit = 2;

                long ret = 1;
                for (int i = 0; i < limit; i++)
                    ret *= (num - i);
                return ret;

            }
            else
            {

                long ret = 1;
                for (int i = 0; i < num - 1; i++)
                    ret *= num - i;
                return ret;
            }
            
        }


        private ReaderWriterLockSlim genWriteLocker = new ReaderWriterLockSlim();


        private void Write(FileStream stream, long position, StringBuilder bob)
        {
            try
            {
                genWriteLocker.EnterWriteLock();

                stream.Position = position;
                char[] chars = new char[bob.Length];
                bob.CopyTo(0, chars, 0, chars.Length);
                bob.Clear();
                byte[] bytes = Encoding.ASCII.GetBytes(chars);
                stream.Write(bytes, 0, bytes.Length);
                //stream.Flush();
                chars = null;
                bytes = null;
            }
            finally
            {
                genWriteLocker.ExitWriteLock();
            }
        }

        private long GetRowsByteLenght(int colLen, int totalLines, bool isAppend, bool isAppendTypeA, int objsAppCount)
        {
            long totalByteLength = 0;
            if (!isAppend)
            {
                totalByteLength = ((colLen * 3) - 1 + 2) * (long)totalLines;//((6 * (each_letter_len + 1_space)) - 1_extra_space + len_\r\n) * totalLines
            }
            //else if (isDifferenct Append)
            //{ 
            //}
            else
            {
                if (isAppendTypeA)
                    totalByteLength = (((colLen + 1) * 3) - 1 + 2) * (long)totalLines;
                else
                    totalByteLength = (((colLen + 1) * 3) - 1 + 2) * (long)totalLines * objsAppCount;
            }
            return totalByteLength;
        }

        private void btnCountingStart_Click(object sender, EventArgs e)
        {
            string inputFilePath = txtCounterInput.Text;
            string rowInput1 = txtCournterRowInput1.Text.Trim();
            string rowInput2 = txtCournterRowInput2.Text.Trim();
            //string rowInput22 = txtCournterRowInput2.Text.Trim();
            bool isTypeA = rdoCounterTypeA.Checked;
            double[] allocArr = new double[15];

            #region Validation

            if (!File.Exists(inputFilePath))
            {
                MessageBox.Show("Input File does not exist!");
                return;
            }
            Match rowInput1Match = Regex.Match(rowInput1, "^\\d{2}(?: \\d{2})*$");
            Match rowInput2Match = Regex.Match(rowInput2, "^\\d{2}(?: \\d{2})*$");// checkout this regx
            if (!rowInput1Match.Success || !rowInput2Match.Success)
            {
                MessageBox.Show("Invalid Row Inputs !!!!!!");

                return;
            }
            foreach (Control c in grpCounter.Controls)
            {
                if (c is TextBox && c.Name.StartsWith("txtCounterAllc"))
                {
                    int i = int.Parse(c.Name.Substring(c.Name.Length - 2, 2)) - 1;
                    double val = -1;
                    try { val = double.Parse(c.Text.Trim()); }
                    catch
                    {
                        MessageBox.Show("Invalid Allocation Value at " + (i + 1));
                        return;
                    }
                    allocArr[i] = val;
                }
            }

            #endregion Validation

            progressSum = 0;
            progress.Maximum = 100;
            progress.Value = 0;
            btnStartBatchFilter.Enabled = false;
            btnGenStart.Enabled = false;
            btnCountingStart.Text = "Stop";
            start = true;

            new Thread(() =>
            {
                DateTime now = DateTime.Now;
                string[] rowInput1Arr = rowInput1.Split(' ');
                string[] rowInput22 = rowInput2.Split(' ');
                FileStream readerFS = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                List<int[]> countingsList = new List<int[]>();
                List<Thread> threads = new List<Thread>();

                for (int j = 0; j < NUM_OF_THREADS; j++)
                {
                    countingsList.Add(new int[15]);
                    Thread t = new Thread(CountingThreadMethod);
                    t.Name = j.ToString();
                    t.Start(new
                    {
                        isTypeA,
                        readerFS,
                        rowInput1Arr,
                        rowInput2,
                        rowInput22,
                        // lbMatchingNumbers,//(Matt Edition)
                        progressVal = 100 / (double)new FileInfo(inputFilePath).Length,
                        countingsArr = countingsList[j]
                    });
                    threads.Add(t);
                }

                foreach (Thread t in threads)
                    t.Join();
                readerFS.Close();
                readerFS.Dispose();

                int[] countingTotalArr = new int[15];
                foreach (int[] cArr in countingsList)
                {
                    for (int i = 0; i <= 14; i++)
                        countingTotalArr[i] += cArr[i];
                    //lbMatchingNumbers.Items.Add(cArr);
                }

                this.Invoke((MethodInvoker)delegate
                {
                    double mainTotal = 0;
                    for (int i = 0; i <= 14; i++)
                    {
                        double allocTotal = countingTotalArr[i] * allocArr[i];
                        mainTotal += allocTotal;
                        listView1.Items[i].SubItems[2].Text = countingTotalArr[i].ToString();
                        listView1.Items[i].SubItems[4].Text = allocTotal.ToString("0 000.00");
                    }
                    txtCounterTotal.Text = mainTotal.ToString("0 000.00");



                });

                bool tmpStart = start;
                start = false;
                this.Invoke(new Action(() =>
                {
                    progress.Value = progress.Maximum;
                    btnCountingStart.Text = "Start";
                    btnStartBatchFilter.Enabled = true;
                    btnGenStart.Enabled = true;
                    btnStartBatchFilter.Enabled = true;
                }));

                if (!tmpStart)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblStatus.Text = "# Stopped by User!";
                    }));
                }
                else
                {
                    this.Invoke(new Action(() =>
                    {
                        GC.Collect();
                        GC.GetTotalMemory(true);
                        MessageBox.Show(this, "Done in " + DateTime.Now.Subtract(now).TotalSeconds.ToString("0.00") + " sec");
                    }));
                }
            }).Start();
        }

        private void CountingThreadMethod(object obj)
        {
            bool isTypeA = ((dynamic)obj).isTypeA;
            FileStream readerFS = ((dynamic)obj).readerFS;
            string[] rowInput1Arr = ((dynamic)obj).rowInput1Arr;
            string[] rowInput22 = ((dynamic)obj).rowInput22;
            string rowInput2 = ((dynamic)obj).rowInput2;
            double progressVal = ((dynamic)obj).progressVal;
            int[] countingArr = ((dynamic)obj).countingsArr;


            while (start)
            {
                StringBuilder bob = this.ReadInputFile(readerFS);
                List<string[]> inputRows = this.ReadRowColumnsFromText(bob);
                int bobLen = bob.Length;
                bob.Clear();
                if (bobLen == 0 || !start)
                    break;

                foreach (string[] row in inputRows)
                {
                    int count = 0;
                    if (isTypeA)
                    {
                        foreach (string inp1col in rowInput1Arr.Intersect(row))
                        {
                            count++;

                        }
                    }


                    else
                    {
                        if (row.Count() >= 5)

                        {
                            List<string> colList1 = row.ToList();
                            foreach (string inp1col in rowInput1Arr)
                            {
                                for (int i = 0; i < colList1.Count-1; i++)////it has to be Count -1
                                {
                                    if (inp1col == colList1[i])
                                    {
                                        colList1.RemoveAt(i);
                                        i--;
                                        count++;
                                        break;
                                    }
                                }
                            }
                        }

                        else

                        {

                            foreach (string inp1col in rowInput1Arr)
                            {
                                foreach (string col in row)
                                {
                                    if (inp1col == col)
                                    {
                                        count++;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    int found = 0;
                    if (isTypeA)
                    {
                        for (int i = 0; i < rowInput22.Length; i++)
                        {
                            foreach (string col in row)
                            {
                                if (int.Parse(rowInput22[i]) == int.Parse(col))
                                {
                                    found++;
                                    break;
                                }
                            }
                        }
                    }
                    else

                    {

                        List<string> colList = row.ToList();


                        for (int i = 0; i < rowInput22.Length; i++)
                        {
                            if (int.Parse(colList.Last()) == int.Parse(rowInput22[i]))
                            {
                                found++;
                                break;
                            }
                        }

                    }
                    try
                    {
                        switch (count)///
                        {
                            case 0:
                                switch (found)
                                {

                                    case 1:
                                        countingArr[14]++;
                                        break;

                                }
                                break;

                            case 1:
                                switch (found)
                                {

                                    case 1:
                                        countingArr[13]++;
                                        break;
                                    case 2:
                                        countingArr[10]++;
                                        break;

                                }
                                break;

                            case 2:
                                switch (found)
                                {

                                    case 0:
                                        countingArr[12]++;
                                        break;
                                    case 1:
                                        countingArr[11]++;
                                        break;
                                    case 2:
                                        countingArr[07]++;
                                        break;

                                }
                                break;

                            case 3:
                                switch (found)
                                {

                                    case 0:
                                        countingArr[9]++;
                                        break;
                                    case 1:
                                        countingArr[8]++;
                                        break;

                                }
                                break;

                            case 4:
                                switch (found)
                                {

                                    case 0:
                                        countingArr[6]++;
                                        break;
                                    case 1:
                                        countingArr[5]++;
                                        break;
                                    case 2:
                                        countingArr[4]++;
                                        break;

                                }
                                break;

                            case 5:
                                switch (found)
                                {

                                    case 0:
                                        countingArr[3]++;
                                        break;
                                    case 1:
                                        countingArr[2]++;
                                        break;
                                    case 2:
                                        countingArr[0]++;
                                        break;

                                }
                                break;

                            case 6:
                                switch (found)
                                {

                                    case 0:
                                        countingArr[1]++;
                                        break;

                                }
                                break;

                        }
                    }
                    catch { }
                }

                this.DoProgress(progressVal * bobLen);
            }
        }


        private void btnCounterBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.RestoreDirectory = true;
            ofd.Multiselect = false;
            ofd.CheckFileExists = false;
            ofd.CheckPathExists = false;
            ofd.Title = "Please choose a input file path :";
            ofd.Filter = "Input File|*.txt";
            DialogResult result = ofd.ShowDialog();
            if (result == DialogResult.OK)
            {
                txtCounterInput.Text = ofd.FileName;
                txtCounterInput.Select(txtCounterInput.Text.Length, 0);
            }
        }

        private void listView1_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = listView1.Columns[e.ColumnIndex].Width;
        }

        private void grpGroupRowColFilter_Enter(object sender, EventArgs e)
        {

        }

        private void textBox37_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {

        }


        private void lbMatchingNumbers_SelectedIndexChanged(object sender, EventArgs e)
        {



        }

        private void txtGroupMemC14G4_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC18G5_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC6G5_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC9G2_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC13G3_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC14G5_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC16G5_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtBatchRows_TextChanged(object sender, EventArgs e)
        {

        }

        private void numBatchMinMatch_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numTargetCols_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chkRowSum_CheckedChanged(object sender, EventArgs e)
        {
            if (chkRangeSumVals.Checked == true)
            {
                chkFixedVal.Checked = false;
                numFixedValue.Enabled = false;
                //numMaximum.Enabled = false;
            }
            else
            {
                numFixedValue.Enabled = true;
                // numMaximum.Enabled = true;
            }


        }

        private void txtGroupMemC10G5_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtGroupMemC10G6_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtFixedVal_TextChanged(object sender, EventArgs e)
        {
            if (chkRangeSumVals.Checked == true)
            {
                numFixedValue.Enabled = false;

            }
        }

        private void chkBatchA_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (chkRangeSumVals.Checked == true)
            {
                chkFixedVal.Checked = false;
                numFixedValue.Enabled = false;
                //numMaximum.Enabled = false;
            }
            else
            {
                numFixedValue.Enabled = true;
                // numMaximum.Enabled = true;
            }
        }

        private void numFixedVal_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numSumofirst2ColsUpper_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numfirst2Cols_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chkBatchScanFilter_CheckedChanged(object sender, EventArgs e)
        {



        }

        private void chkRangeSums_CheckedChanged(object sender, EventArgs e)
        {

            if (chkRangeSums.Checked == true)
            {
                chkColSum.Checked = false;
                chkBatchGroup.Checked = false;
                chkSummationFilter.Checked = true;
                chkBatchScanFilter.Checked = false;

            }
            else
            {
                chkRangeSums.Checked = false;

            }

        }

        private void numMinMatch_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chkColSum_CheckedChanged(object sender, EventArgs e)
        {


            if (chkColSum.Checked == true)
            {
                chkBatchScanFilter.Checked = false;
                chkSummationFilter.Checked = true;
                chkRangeSums.Checked = false;
                chkBatchGroup.Checked = false;

            }
            else
            {
                chkColSum.Checked = false;

            }
        }

        private void chkBatchGroup_CheckedChanged(object sender, EventArgs e)
        {

            if (chkBatchGroup.Checked == true)
            {
                chkColSum.Checked = false;
                chkRangeSums.Checked = false;
                chkSummationFilter.Checked = true;

            }
            else
            {
                chkBatchGroup.Checked = false;

            }
        }

        private void chkMatchedSum_CheckedChanged(object sender, EventArgs e)
        {

            if (chkMatchedSum.Checked == true)
            {

                chkunMatchedSum.Checked = false;

            }
            else
            {
                chkMatchedSum.Checked = false;

            }

        }

        private void chkunMatchedSum_CheckedChanged(object sender, EventArgs e)
        {
            if (chkunMatchedSum.Checked == true)
            {

                chkMatchedSum.Checked = false;

            }
            else
            {
                chkunMatchedSum.Checked = false;

            }
        }

        private void txtBrowseInput_TextChanged(object sender, EventArgs e)
        {
        }
        private void txtCounterInput_TextChanged(object sender, EventArgs e)
        {

        }

        private void numFixedValue_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chkPowerMatch_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPowerMatch.Checked == true)//////////////////////
            {
                chkPowerOpt.Checked = false;

            }
            else
            {
                chkPowerMatch.Checked = false;
            }

        }

        private void chkMaximise_CheckedChanged(object sender, EventArgs e)
        {

            if (chkMaximise.Checked == true)
            {
                chkMedium.Checked = false;
                chkMinimum.Checked = false;


            }
            else
            {
                chkMaximise.Checked = false;

            }
        }

        private void chkMidium_CheckedChanged(object sender, EventArgs e)
        {
            if (chkMedium.Checked == true)
            {
                chkMaximise.Checked = false;
                chkMinimum.Checked = false;

            }
            else
            {
                chkMedium.Checked = false;

            }
        }

        private void chkMinimum_CheckedChanged(object sender, EventArgs e)
        {

            if (chkMinimum.Checked == true)
            {
                chkMaximise.Checked = false;
                chkMedium.Checked = false;
            }

            else
            {
                chkMinimum.Checked = false;
            }



        }

        private void numSumOfirst5ColsUpper_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numericUpDown7_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numFirst3C_ValueChanged(object sender, EventArgs e)
        {

        }

        private void numSumOfirst3ColsUpper_ValueChanged(object sender, EventArgs e)
        {

        }

        private void lblComb62_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            combDic[lbl] = !state;
        }

        private void lblComb53_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            combDic[lbl] = !state;
        }

        private void lblComb54_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            combDic[lbl] = !state;
        }

        private void lblComb63_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            bool state = combDic[lbl];
            if (!state)
            {
                lbl.BackColor = selectColor;
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = normalColor;
                lbl.ForeColor = Color.Black;
            }
            combDic[lbl] = !state;
        }

        private void numFirst4A_ValueChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

            if (chkClampedOpt2.Checked == true)
            {
                chkClampedOpt1.Checked = false;
            }
            else
            {
                chkClampedOpt2.Checked = false;
            }

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }

        private void txtGrpRowColA8_TextChanged(object sender, EventArgs e)
        {

        }

        private void numPowermin5_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chkPowerOpt_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPowerOpt.Checked == true)
            {

                chkPowerMatch.Checked = false;
            }

        }

        private void numFirst4AUpper_ValueChanged(object sender, EventArgs e)
        {

        }

        private void txtCounterAllc10_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtCounterAllc07_TextChanged(object sender, EventArgs e)
        {

        }

        private void rdoCounterTypeA_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void chkTwoPlus_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void txtCounterTotal_TextChanged(object sender, EventArgs e)
        {
        }

        private void chkBaseFiltr_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBaseFiltr.Checked == true)
            {
                chkPowRestFltr.Checked = false;
                chkDailyFltr.Checked = false;

            }
            else
            {
                chkBaseFiltr.Checked = false;

            }
        }

        private void chkDailyFltr_CheckedChanged(object sender, EventArgs e)
        {

            if (chkDailyFltr.Checked == true)
            {
                chkPowRestFltr.Checked = false;
                chkBaseFiltr.Checked = false;

            }
            else
            {
                chkDailyFltr.Checked = false;

            }

        }

        private void chkPowRestFltr_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPowRestFltr.Checked == true)
            {
                chkDailyFltr.Checked = false;
                chkBaseFiltr.Checked = false;

            }
            else
            {
                chkPowRestFltr.Checked = false;

            }
        }

        private void batchRowsPowa_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label96_Click(object sender, EventArgs e)
        {

        }
    }
}
