/*
* ==============================================================================
* Filename: SoftwareReleaseNote
* Description: 
*
*  ReleaseNote for logging software
* 
* Version: 1.0
* Created: 2023/4/1 10:18:36
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.EnumHelp;
using ArbinUtil.Jira;
using Grynwald.MarkdownGenerator;
using System.Collections.Generic;
using System.Text;
using static ArbinUtil.PSCommand.GitGetJiraIssueReleaseNoteCommand;
using static ArbinUtil.Util;

namespace ArbinUtil
{
    public class SoftwareReleaseNote
    {
        public List<JiraLikeMessage>[] Groups { get; }
        public string SoftwareName { get; set; } = "Software";

        public SoftwareReleaseNote(JiraLikeMessage[] source) 
        {
            Groups = JiraUtil.LikeLabelGroupApplySortJiraLikeMessage(source);
        }

        public static string GeneratingMarkDownReleaseNote(params SoftwareReleaseNote[] releaseNotes)
        {
            var document = new MdDocument();
            List<MdBlock> blocks = new List<MdBlock>(); 
            int softwareCount = releaseNotes.Length;
            for(LikeLabelName i = 0; i < LikeLabelName.MaxCount; i++)
            {
                for(int j = 0; j < softwareCount; j++)
                {
                    var note = releaseNotes[j];
                    var group = note.Groups[(int)i];
                    int itemCount = group.Count;
                    if(itemCount == 0)
                        continue;
                    blocks.Add(new MdHeading($"{note.SoftwareName}", 3));
                    var list = new MdOrderedList();
                    blocks.Add(list);
                    for(int k = 0; k < itemCount; k++)
                    {
                        var item = group[k];
                        list.Add(new MdListItem(new MdParagraph($"{item.ReleaseNote}({item.Key})")));
                    }
                }
                if(blocks.Count <= 0)
                    continue;
                document.Root.Add(new MdHeading($"{i.GetDescription()}", 2));
                for(int k = 0; k < blocks.Count; k++)
                {
                    document.Root.Add(blocks[k]);
                }
                blocks.Clear();
            }

            return document.ToString();
        }

        //public static string GeneratingMarkDownReleaseNote(params SoftwareReleaseNote[] releaseNotes)
        //{
        //    StringBuilder result = new StringBuilder();
        //    StringBuilder temp = new StringBuilder();
        //    int softwareCount = releaseNotes.Length;
        //    for (LikeLabelName i = 0; i < LikeLabelName.MaxCount; i++)
        //    {
        //        for (int j = 0; j < softwareCount; j++)
        //        {
        //            var note = releaseNotes[j];
        //            var group = note.Groups[(int)i];
        //            int itemCount = group.Count;
        //            if (itemCount == 0)
        //                continue;
        //            temp.AppendLine($"### {note.SoftwareName}");
        //            for (int k = 0; k < itemCount; k++) 
        //            {
        //                var item = group[k];
        //                temp.AppendLine($"1. {item.ReleaseNote}({item.Key})");
        //            }
        //        }
        //        if (temp.Length <= 0)
        //            continue;
        //        result.AppendLine($"## {i.GetDescription()}");
        //        result.Append(temp);
        //        result.AppendLine();
        //        temp.Clear();
        //    }

        //    return result.ToString();
        //}
    }
}
