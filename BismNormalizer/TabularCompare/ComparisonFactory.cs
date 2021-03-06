﻿using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using BismNormalizer.TabularCompare.Core;

namespace BismNormalizer.TabularCompare
{
    /// <summary>
    /// Class for instantiation of Core.Comparison objects using simple factory design pattern.
    /// </summary>
    public static class ComparisonFactory
    {
        // Factory pattern: https://msdn.microsoft.com/en-us/library/orm-9780596527730-01-05.aspx 

        //private static List<int> _supportedCompatibilityLevels = new List<int>() { 1100, 1103, 1200, 1400 };
        private static int _minCompatibilityLevel = 1100;
        private static int _maxCompatibilityLevel = 1499;
        private static List<string> _supportedDataSourceVersions = new List<string> { "PowerBI_V3" };

        /// <summary>
        /// Uses factory design pattern to return an object of type Core.Comparison, which is instantiated using MultidimensionalMetadata.Comparison or TabularMeatadata.Comparison depending on SSAS compatibility level. Use this overload when running in Visual Studio.
        /// </summary>
        /// <param name="comparisonInfo">ComparisonInfo object for the comparison.</param>
        /// <param name="userCancelled">If use decides not to close .bim file(s) in Visual Studio, returns true.</param>
        /// <returns>Core.Comparison object</returns>
        public static Comparison CreateComparison(ComparisonInfo comparisonInfo, out bool userCancelled)
        {
            //This overload is for running in Visual Studio, so can set PromptForDatabaseProcessing = true
            comparisonInfo.PromptForDatabaseProcessing = true;

            // Need to ensure compatibility levels get initialized here (instead of comparisonInfo initialization properties). This also serves to prep databases on workspace server while finding compatibility levels
            comparisonInfo.InitializeCompatibilityLevels(out userCancelled);
            if (userCancelled)
            {
                return null;
            }

            return CreateComparisonInitialized(comparisonInfo);
        }

        /// <summary>
        /// Uses factory design pattern to return an object of type Core.Comparison, which is instantiated using MultidimensionalMetadata.Comparison or TabularMeatadata.Comparison depending on SSAS compatibility level.
        /// </summary>
        /// <param name="bsmnFile">Full path to the BSMN file.</param>
        /// <returns>Core.Comparison object</returns>
        public static Comparison CreateComparison(string bsmnFile)
        {
            ComparisonInfo comparisonInfo = ComparisonInfo.DeserializeBsmnFile(bsmnFile);
            return CreateComparison(comparisonInfo);
        }

        /// <summary>
        /// Uses factory design pattern to return an object of type Core.Comparison, which is instantiated using MultidimensionalMetadata.Comparison or TabularMeatadata.Comparison depending on SSAS compatibility level.
        /// </summary>
        /// <param name="comparisonInfo">ComparisonInfo object for the comparison.</param>
        /// <returns>Core.Comparison object</returns>
        public static Comparison CreateComparison(ComparisonInfo comparisonInfo)
        {
            comparisonInfo.InitializeCompatibilityLevels();
            return CreateComparisonInitialized(comparisonInfo);
        }

        private static Comparison CreateComparisonInitialized(ComparisonInfo comparisonInfo)
        {
            //todo: delete ------------------------------------------
            //if (comparisonInfo.SourceCompatibilityLevel != comparisonInfo.TargetCompatibilityLevel && !(comparisonInfo.SourceCompatibilityLevel == 1200 && comparisonInfo.TargetCompatibilityLevel >= 1400))
            //{
            //    throw new ConnectionException($"This combination of mixed compatibility levels is not supported.\nSource is {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)} and target is {Convert.ToString(comparisonInfo.TargetCompatibilityLevel)}.");
            //}

            //Todo: fix this for composite models:
            if (comparisonInfo.SourceDirectQuery != comparisonInfo.TargetDirectQuery)
            {
                throw new ConnectionException($"Mixed DirectQuery settings are not supported.\nSource is {(comparisonInfo.SourceDirectQuery ? "On" : "Off")} and target is {(comparisonInfo.TargetDirectQuery ? "On" : "Off")}.");
            }

            ////We know both models have same compatibility level, but is it supported?
            //if (!_supportedCompatibilityLevels.Contains(comparisonInfo.SourceCompatibilityLevel))
            //{
            //    throw new ConnectionException($"Models have compatibility level of {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)}, which is not supported by this version.");
            //}
            //-------------------------------------------------------


            //If Power BI, check the default datasource version
            //Source
            if (comparisonInfo.ConnectionInfoSource.ServerName.StartsWith("powerbi://") &&
                !_supportedDataSourceVersions.Contains(comparisonInfo.SourceDataSourceVersion))
            {
                throw new ConnectionException($"Source model is a Power BI dataset with default data-source version (basically the dataset metadata format) of {comparisonInfo.SourceDataSourceVersion}, which is not supported for comparison.");
            }
            //Target
            if (comparisonInfo.ConnectionInfoTarget.ServerName.StartsWith("powerbi://") &&
                !_supportedDataSourceVersions.Contains(comparisonInfo.TargetDataSourceVersion))
            {
                throw new ConnectionException($"Target model is a Power BI dataset with default data-source version (basically the dataset metadata format) of {comparisonInfo.TargetDataSourceVersion}, which is not supported for comparison.");
            }


            //Check if one of the supported compat levels:
            if (
                   !(comparisonInfo.SourceCompatibilityLevel >= _minCompatibilityLevel && comparisonInfo.SourceCompatibilityLevel <= _maxCompatibilityLevel &&
                     comparisonInfo.TargetCompatibilityLevel >= _minCompatibilityLevel && comparisonInfo.TargetCompatibilityLevel <= _maxCompatibilityLevel
                    )
               )
            {
                throw new ConnectionException($"This combination of mixed compatibility levels is not supported.\nSource is {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)} and target is {Convert.ToString(comparisonInfo.TargetCompatibilityLevel)}.");
            }

            //Return the comparison object & offer upgrade of target if appropriate
            Comparison returnComparison = null;

            if (comparisonInfo.SourceCompatibilityLevel >= 1200)
            {
                returnComparison = new TabularMetadata.Comparison(comparisonInfo);

                //Check if target has a higher compat level than the source and offer upgrade if appropriate
                if (comparisonInfo.SourceCompatibilityLevel > comparisonInfo.TargetCompatibilityLevel)
                {
                    if (System.Windows.Forms.MessageBox.Show($"Source compatibility level is {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)} and target is {Convert.ToString(comparisonInfo.TargetCompatibilityLevel)}, which is not supported for comparison.\n\nDo you want to upgrade the target to {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)}?", "ALM Toolkit", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                    {
                        TabularMetadata.Comparison returnTabularComparison = (TabularMetadata.Comparison)returnComparison;
                        returnTabularComparison.TargetTabularModel.Connect();
                        returnTabularComparison.TargetTabularModel.TomDatabase.CompatibilityLevel = comparisonInfo.SourceCompatibilityLevel;
                        returnTabularComparison.TargetTabularModel.TomDatabase.Update();
                        returnTabularComparison.Disconnect();
                    }
                    else
                    {
                        throw new ConnectionException($"This combination of mixed compatibility levels is not supported.\nSource is {Convert.ToString(comparisonInfo.SourceCompatibilityLevel)} and target is {Convert.ToString(comparisonInfo.TargetCompatibilityLevel)}.");
                    }
                }
            }
            else
            {
                returnComparison = new MultidimensionalMetadata.Comparison(comparisonInfo);
            }

            return returnComparison;
        }
    }
}
