//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace NumberProcessor
//{
//    internal class RefinesMainInputArrays
//    {
//        public RefinesMainInputArrays(List<string[]> mainInputArrays, List<string[]> batchArrays0, int matchCount0,
//                     int numDelinCount0, List<string[]> batchArrays1, int matchCount1, int numDelinCount1,
//                        List<string[]> batchArrays2, int matchCount2, int numDelinCount2, List<string[]> batchArrays3,
//                        int matchCount3, int numDelinCount3, string[] guidingStrings0, string[] guidingStrings1)

//        {

//            for (int i = 0; i < mainInputArrays.Count(); i++)
//            {
              
//                string[] mainInputArray = mainInputArrays[i];

//                _RefinesMainInputArrays = ProcessFilterArrays1(
//                        mainInputArray[i],
//                        batchArrays0,
//                        matchCount0,
//                        numDelinCount0,
//                        guidingStrings0
//                       );

//                matchingFilterArraysCounter1 = ProcessFilterArrays1(
//                        mainInputArray[i],
//                        batchArrays1,
//                        matchCount1,
//                        numDelinCount1,
//                        guidingStrings1);


//                matchingFilterArraysCounter2 = ProcessFilterArrays2(
//                       mainInputArray[i],
//                       batchArrays2,
//                       guidingStrings1,
//                       matchingFilterArraysCounter2,
//                       requiredNumberOfMatchingElements2);

//                matchingFilterArraysCounter3 = ProcessFilterArrays3(
//                       mainInputArray[i],
//                       batchArrays3,
//                       guidingStrings1,
//                       matchingFilterArraysCounter3,
//                       requiredNumberOfMatchingElements3);

//                mainOutput[i].Add(mainInputArray[i]);
//            }
//        }




//    }
//}
//}
