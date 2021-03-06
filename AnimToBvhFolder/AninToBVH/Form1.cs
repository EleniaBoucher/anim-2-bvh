using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Globalization;


namespace AninToBVH
{
    public partial class Form1 : Form
    {
        Translation trans = new Translation();

        StatusCode Status = new StatusCode();
        string[] aFiles;

        public Form1()
        {
            if (!(trans.mStatus == Status.ST_OK))
            {
                MessageBox.Show(trans.mStatus);
                Environment.Exit(0);
            }
            InitializeComponent();
        }
        private void bAnim_Click(object sender, EventArgs e)
        {
            labelSuccess.Text = "";
            openFileDialog1.Filter = "animatn files (*.animatn)|*.animatn|anim files (*.anim)|*.anim|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                aFiles = openFileDialog1.FileNames;
                string xAnim = Path.GetFileNameWithoutExtension(aFiles[0]);
                wAnim.Text = aFiles[0];
                wBVH.Text = Path.GetDirectoryName(wAnim.Text);
                wFileBVH.Text = xAnim + ".bvh";
            }
            pConverti.Enabled = PulsEseguiOk();
        }

        private void bBVH_Click(object sender, EventArgs e)
        {
            labelSuccess.Text = "";
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                wBVH.Text = folderBrowserDialog1.SelectedPath;
            }
            pConverti.Enabled = PulsEseguiOk();
        }
        private bool PulsEseguiOk()
        {
            if ((wAnim.Text.Length > 0) && (wBVH.Text.Length > 0) && (wFileBVH.Text.Length > 0))
                return true;
            else
                return false;
        }

        private void pConverti_Click(object sender, EventArgs e)
        {
            pConverti.Enabled = false;
            bBVH.Enabled = false;
            bAnim.Enabled = false;
            for (int ii = 0; ii < aFiles.Length; ++ii)
            {
                string xAnim = Path.GetFileNameWithoutExtension(aFiles[ii]);
                wAnim.Text = aFiles[ii];
                wBVH.Text = Path.GetDirectoryName(wAnim.Text);
                wFileBVH.Text = xAnim + ".bvh";

                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(aFiles[ii]);
                    // Read the file .anim and creates the structure MyBinBVH
                    // Use the class myBinBVHAnimationReader OpenMetaverse (amended). 
                    // The structure contains the data formats .anim already serialized
                    myBinBVHAnimationReader MyBinBVH = new myBinBVHAnimationReader(fileData);
                    uint numJoints = MyBinBVH.JointCount;
                    // Reads the key position (x) of the first frame of the first joint
                    // By definition belongs to the  frame 2.
                    // Frame time ==  second frame time/2. 
                    double wTime = MyBinBVH.joints[0].rotationkeys[0].time;
                    double mFrameTime = wTime * 0.5;
                    // dividing the total duration (lentgh) by the frame time 
                    // the number of frames is obtained.
                    double mDuration = MyBinBVH.Length;
                    uint mNumFrames = (uint)Math.Floor(mDuration / mFrameTime + .5);
                    // The class creates a JointBVH structure intended to receive
                    // processed data to transform the Vettore3 of rot 
                    // in degrees and Vector3 of Pos in inches. 
                    // The structure is initialized with Frames*(19)
                    // structures Pos [3], Rot [3] all set to zero, except
                    // Pos[1] of the joint [0], for every frame, is placed
                    // equal to 43.5285 ([Y]  position)
                    JointBVH myTSBVH = new JointBVH((int)mNumFrames);
                    // numJoint contains the number of joint of file .anim
                    // myTSBVH is filled with data read from anim.ini
                    // indent will be used later to indent the BVH file header
                    int[] indent = new int[19] { 1, 2, 3, 4, 5, 4, 5, 6, 7, 4, 5, 6, 7, 2, 3, 4, 2, 3, 4 };
                    uint i;
                    int j = 0;
                    for (i = 0; i < trans.mTranslation.Length; ++i)
                    {
                        if (trans.mTranslation[i].mIgnore)
                        {
                            continue;
                        }
                        myTSBVH.mJoints[j].mName = trans.mTranslation[i].mName;
                        myTSBVH.mJoints[j].mOutName = trans.mTranslation[i].mOutName;
                        myTSBVH.mJoints[j].mIndent = indent[j];
                        ++j;
                    }

                    // Value are processed for each of the joint
                    for (i = 0; i < numJoints; ++i)
                    {
                        uint joint;
                        Order order;
                        Quaternion last_rot = new Quaternion();
                        string jointName = MyBinBVH.joints[i].Name;
                        uint ji;
                        // The joints of the anim file may be in different order
                        // from the BVH Header model used 
                        // in this procedure. It was therefore I decided to carry out
                        // search by the name of the joint.
                        for (ji = 0; ji < 19; ji++)
                        {
                            joint = ji;
                            if (myTSBVH.mJoints[ji].mOutName == jointName) break;
                        }
                        if (ji == 19)
                            continue;
                        else
                            joint = ji;
                        uint last_frame = 1;

                        order = (Order)Enum.Parse(typeof(Order), myTSBVH.mJoints[joint].mOrder);
//                        order = StringToOrder(myTSBVH.mJoints[joint].mOrder);
                        // we analyze every frame of the  file anim joint 
                        for (j = 0; j < (MyBinBVH.joints[i].rotationkeys.Length); ++j)
                        {

                            if (j == 0) // first key 
                            // the first key  of the file anim
                            // refers to the frame 2 as SL eliminates the
                            // frame 1 from the original BVH file
                            {
                                last_frame = 1; // we are in the frame 2
                                // last_rot (first frame) is not available
                                // so we start with an identity quaternion
                                last_rot = Quaternion.Identity;
                                myTSBVH.mJoints[joint].mPosRotKeys[0].mIgnorePos = true;
                                myTSBVH.mJoints[joint].mPosRotKeys[0].mIgnoreRot = true;
                            }
                            // The frame no.is equal to the time of the frame/FrameTime
                            float time_short = MyBinBVH.joints[i].rotationkeys[j].time;
                            double time = time_short;
                            uint frame;
                            frame = (uint)Math.Floor(time / mFrameTime + 0.5f);
                            // rot_vec is set to the rotation vector of the frame examined
                            Vector3 rot_vec;
                            rot_vec.X = MyBinBVH.joints[i].rotationkeys[j].key_element.X;
                            rot_vec.Y = MyBinBVH.joints[i].rotationkeys[j].key_element.Y;
                            rot_vec.Z = MyBinBVH.joints[i].rotationkeys[j].key_element.Z;
                            // we compute the inverse of quaternion framematrix 
                            // and offsetmatrix
                            Quaternion outRot;
                            Quaternion inRot;
                            Quaternion frameRot = myTSBVH.mFrameMatrix;
                            Quaternion frameRotInv;
                            frameRotInv.X = frameRot.X * -1f;
                            frameRotInv.Y = frameRot.Y * -1f;
                            frameRotInv.Z = frameRot.Z * -1f;
                            frameRotInv.W = frameRot.W;

                            Quaternion offsetRot = myTSBVH.mOffsetMatrix;
                            Quaternion offsetRotInv;
                            offsetRotInv.X = offsetRot.X * -1f;
                            offsetRotInv.Y = offsetRot.Y * -1f;
                            offsetRotInv.Z = offsetRot.Z * -1f;
                            offsetRotInv.W = offsetRot.W;
                            // Quaternion outRot is derived from the  vector rot_vec
                            outRot.X = rot_vec.X;
                            outRot.Y = rot_vec.Y;
                            outRot.Z = rot_vec.Z;
                            double t = 1.0 - (rot_vec.X * rot_vec.X + rot_vec.Y * rot_vec.Y + rot_vec.Z * rot_vec.Z);
                            if (t > 0)
                            {
                                outRot.W = (float)Math.Sqrt(t);
                            }
                            else
                            {
                                outRot.W = 0;
                            }
                            // rotation is corrected by the rotation matrices
                            inRot = frameRotInv * offsetRotInv * outRot * frameRot;
                            // recalculates the rotation vector.
                            // revMayaQ returns values in degrees and set the axes
                            // XYZ values based on the contents of the enumerator order
                            rot_vec = revMayaQ(inRot, order);
                            // the vector obtained is saved for use in
                            // when writing the file BVH
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mRot.X = rot_vec.X;
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mRot.Y = rot_vec.Y;
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mRot.Z = rot_vec.Z;
                            // If SL has missed because of little significance,
                            // some frames, they are reconstructed with interpolation
                            // If frames have been skipped by SL, 
                            // frame is greater than last_frame + 1
                            uint n;
                            uint num = frame - last_frame;
                            Quaternion interp = new Quaternion();
                            for (n = last_frame + 1; n < frame; ++n)
                            {
                                // interpolation for each frame missing
                                interp = nlerp((1f / num) * (n - last_frame), last_rot, inRot);
                                // we also need the value in degrees 
                                // revMayaQ returns values in degrees and set the axes
                                // XYZ values based on the contents of the enumerator order
                                rot_vec = revMayaQ(interp, order);
                                // the vector obtained is saved for use in
                                // when writing the file BVH
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mRot.X = rot_vec.X;
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mRot.Y = rot_vec.Y;
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mRot.Z = rot_vec.Z;
                            }
                            last_frame = frame;  // comparison values for the next cycle
                            last_rot = inRot;
                        }
                        // Now we go to test the positions.
                        // the test is done for every joint. The activity is redundant
                        // because when we write the BVH file will take only
                        // the first joint value (hip)
                        Vector3 last_pos = new Vector3();
                        Vector3 current_pos = new Vector3();
                        // rel_key represents the relative position. 
                        // In fact anim file contains the changes with respect 
                        // to the relative position
                        Vector3 relkey = new Vector3(0f, 43.5285f, 0f);
                        for (j = 0; j < (MyBinBVH.joints[i].positionkeys.Length); ++j)
                        {
                            float time_short = MyBinBVH.joints[i].positionkeys[j].time;
                            double time = time_short;
                            // Only for the hip frame 1 contains the value of the relative position.
                            // values for all other joint can remain set to zero.
                            if (j == 0)
                            {
                                last_pos.X = 0f;
                                last_pos.Y = 43.5285f;
                                last_pos.Z = 0;
                                last_frame = 1;
                            }
                            uint frame;
                            frame = (uint)Math.Floor(time / mFrameTime + 0.5f);
                            // 
                            current_pos.X = MyBinBVH.joints[i].positionkeys[j].key_element.X / 0.02540005f;
                            current_pos.Y = MyBinBVH.joints[i].positionkeys[j].key_element.Y / 0.02540005f;
                            current_pos.Z = MyBinBVH.joints[i].positionkeys[j].key_element.Z / 0.02540005f;
                            // All we need is the frame matrix and its inverse
                            Quaternion frameRot = myTSBVH.mFrameMatrix;
                            Quaternion frameRotInv;
                            frameRotInv.X = -frameRot.X;
                            frameRotInv.Y = -frameRot.Y;
                            frameRotInv.Z = -frameRot.Z;
                            frameRotInv.W = frameRot.W;
                            // 
                            current_pos = (current_pos * frameRotInv) + relkey;
                            // 
                            // 
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mPos.X = current_pos.X;
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mPos.Y = current_pos.Y;
                            myTSBVH.mJoints[joint].mPosRotKeys[frame - 1].mPos.Z = current_pos.Z;
                            // 
                            // 
                            uint n;
                            uint num = frame - last_frame;
                            Vector3 interp;
                            for (n = last_frame + 1; n < frame; ++n)
                            {
                                interp = Vector3.Lerp(last_pos, current_pos, 1f / num * (n - last_frame));
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mPos.X = interp.X;
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mPos.Y = interp.Y;
                                myTSBVH.mJoints[joint].mPosRotKeys[n - 1].mPos.Z = interp.Z;
                            }
                            // 
                            last_pos = current_pos;
                            last_frame = frame;
                        }
                    }
                    // all values were calculated
                    // we can and write the file BVH
                    string nRotation = "rotation ";
                    string nOffset = "OFFSET ";
                    string nChannels = "CHANNELS 3 ";
                    string nJoint = "JOINT ";
                    string nEnd = "End Site";
                    char nOpenP = '{';
                    char nCloseP = '}';
                    char nTab = '\t';
                    // anim file does not contain the offset value to the joints
                    // We use the following standard.
                    // The BVH file is Text format, so it is useless
                    // use numeric values
                    string[] aOffset = new string[19]
                 {
                     "0.000000 0.000000 0.000000",
                     "0.000000 3.422050 0.000000",
                     "0.000000 8.486693 -0.684411",
                     "0.000000 10.266162 -0.273764",
                     "0.000000 3.148285 0.000000",
                     "3.422053 6.707223 -0.821293",
                     "3.285171 0.000000 0.000000",
                     "10.129278 0.000000 0.000000",
                     "8.486692 0.000000 0.000000",
                     "-3.558935 6.707223 -0.821293",
                     "-3.148289 0.000000 0.000000",
                     "-10.266159 0.000000 0.000000",
                     "-8.349810 0.000000 0.000000",
                     "5.338403 -1.642589 1.368821",
                     "-2.053232 -20.121670 0.000000",
                     "0.000000 -19.300380 -1.231939",
                     "-5.338403 -1.642589 1.368821",
                     "2.053232 -20.121670 0.000000",
                     "0.000000 -19.300380 -1.231939"
                 };
                    // Offset value to the 5 knots End Side
                    string[] aOffsetEnd = new string[5]
                 {
                     "0.000000 3.148289 0.000000",
                     "4.106464 0.000000 0.000000",
                     "-4.106464 0.000000 0.000000",
                     "0.000000 -2.463878 4.653993",
                     "0.000000 -2.463878 4.653993"
                 };
                    StringBuilder llsd = new StringBuilder();
                    string NL = "\r\n";
                    llsd.Append("HIERARCHY" + NL);
                    // The hip joint is special -  use ROOT instead of  JOINT
                    llsd.Append("ROOT hip" + NL);
                    llsd.Append(nOpenP + NL);
                    llsd.Append(nTab + nOffset + aOffset[0] + NL);
                    // CHANNELS for the hip joint is special
                    llsd.Append("\tCHANNELS 6 Xposition Yposition Zposition Xrotation Zrotation Yrotation " + NL);
                    int wExInd = 0;
                    int wInd;
                    j = 0;
                    // We start from the second joint - hip has already been created.
                    for (i = 1; i < myTSBVH.mJoints.Length; ++i)
                    {
                        wInd = myTSBVH.mJoints[i].mIndent;
                        // End Side is identified with the transition to a lower indent
                        if (wInd < wExInd)      // End Side
                        {
                            llsd.Append(nTab, wExInd); llsd.Append(nEnd); llsd.Append(NL);
                            llsd.Append(nTab, wExInd); llsd.Append(nOpenP); llsd.Append(NL);
                            llsd.Append(nTab, wExInd + 1); llsd.Append(nOffset); llsd.Append(aOffsetEnd[j]); llsd.Append(NL);
                            ++j;
                            // The difference with the previous indent determines the number of "}"
                            for (int vj = wExInd; vj > wInd - 2; --vj)
                            {
                                llsd.Append(nTab, vj); llsd.Append(nCloseP); llsd.Append(NL);
                            }
                        }
                        //  It' a new Joint - we proceed to write
                        llsd.Append(nTab, wInd - 1); llsd.Append(nJoint); llsd.Append(myTSBVH.mJoints[i].mName); llsd.Append(NL);
                        llsd.Append(nTab, wInd - 1); llsd.Append(nOpenP); llsd.Append(NL);
                        llsd.Append(nTab, wInd); llsd.Append(nOffset); llsd.Append(aOffset[i]); llsd.Append(NL);
                        llsd.Append(nTab, wInd); llsd.Append(nChannels);
                        // The order of rotations following the arbitrary default
                        llsd.Append(myTSBVH.mJoints[i].mOrder[0]); llsd.Append(nRotation);
                        llsd.Append(myTSBVH.mJoints[i].mOrder[1]); llsd.Append(nRotation);
                        llsd.Append(myTSBVH.mJoints[i].mOrder[2]); llsd.Append(nRotation); llsd.Append(NL);
                        wExInd = wInd;
                    }
                    // This is the last End Side
                    llsd.Append(nTab, wExInd); llsd.Append(nEnd); llsd.Append(NL);
                    llsd.Append(nTab, wExInd); llsd.Append(nOpenP); llsd.Append(NL);
                    llsd.Append(nTab, wExInd + 1); llsd.Append(nOffset); llsd.Append(aOffsetEnd[j]); llsd.Append(NL);
                    for (int vj = wExInd + 1; vj > 0; --vj)
                    {
                        llsd.Append(nTab, vj - 1); llsd.Append(nCloseP); llsd.Append(NL);

                    }
                    // Once the structure of the Joints starts listing the frames

                    llsd.Append("MOTION" + NL);

                    llsd.Append("Frames: "); llsd.Append((int)mNumFrames); llsd.Append(NL);
                    if (mNumFrames * mFrameTime > 60f) // not more then 60sec animation time
                        mFrameTime = 60f / mNumFrames;
                    llsd.Append("Frame Time: "); llsd.Append(NormStr((float)mFrameTime)); llsd.Append(NL);

                    for (i = 0; i < mNumFrames; ++i)
                    {
                        uint ji;
                        for (ji = 0; ji < 19; ++ji)
                        {
                            if (ji == 0) // position values for only the first joint (hip)
                            {
                                llsd.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[i].mPos.X)); llsd.Append(" ");
                                llsd.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[i].mPos.Y)); llsd.Append(" ");
                                llsd.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[i].mPos.Z)); llsd.Append(" ");
                            }
                            // Rotations are arranged by mOrder
                            string s = myTSBVH.mJoints[ji].mOrder;
                            char[] c = s.ToCharArray();
                            float[] wRot = new float[3];
                            wRot[0] = myTSBVH.mJoints[ji].mPosRotKeys[i].mRot.X;
                            wRot[1] = myTSBVH.mJoints[ji].mPosRotKeys[i].mRot.Y;
                            wRot[2] = myTSBVH.mJoints[ji].mPosRotKeys[i].mRot.Z;
                            // whim of the programmer 'X' - 'X' = 0, 'Y'- 'X' = 1, 'Z' - 'X' = 2
                            llsd.Append(NormStr(wRot[c[0] - 'X']));
                            llsd.Append(" ");
                            llsd.Append(NormStr(wRot[c[1] - 'X']));
                            llsd.Append(" ");
                            llsd.Append(NormStr(wRot[c[2] - 'X']));
                            llsd.Append(" ");
                        }
                        llsd.Append(NL);
                    }

                    string uAnim = Path.Combine(wBVH.Text, wFileBVH.Text);

                    File.WriteAllText(uAnim, llsd.ToString());
                    //
                    // We create a second file with .txt ext.
                    // Save the data that help to upload the BVH file
                    string xFileName = uAnim.Replace(".bvh",".txt");

                    StringBuilder llse = new StringBuilder();
                    llse.Append("\t\t File "); llse.Append(wFileBVH.Text); llse.Append(NL);
                    llse.Append(NL);
                    llse.Append("Upload Informations"); llse.Append(NL);
                    llse.Append("-------------------"); llse.Append(NL);
                    llse.Append("Priority\t : "); llse.Append(MyBinBVH.Priority); llse.Append(NL);
                    llse.Append("Loop \t\t : "); llse.Append(MyBinBVH.Loop == true ? "Yes" : "Not"); llse.Append(NL);
                    if (MyBinBVH.Loop == true)
                    {
                        llse.Append("Loop in(%)\t : "); llse.Append((uint)Math.Floor(MyBinBVH.InPoint * 100 / MyBinBVH.Length + 0.5));
                        llse.Append("\tOut(%)\t : "); llse.Append((uint)Math.Floor(MyBinBVH.OutPoint * 100 / MyBinBVH.Length + 0.5)); llse.Append(NL);
                    }
                    llse.Append("Hand Pose\t : "); llse.Append(HandPose(MyBinBVH.HandPose)); llse.Append(NL);
                    llse.Append("Expression\t : "); llse.Append(MyBinBVH.ExpressionName); llse.Append(NL);
                    llse.Append("Ease in(sec)\t : "); llse.Append(MyBinBVH.EaseInTime);
                    llse.Append("\tOut(sec) : "); llse.Append(MyBinBVH.EaseOutTime); llse.Append(NL);
                    llse.Append(NL);
                    llse.Append("Usefull Information"); llse.Append(NL);
                    llse.Append("-------------------"); llse.Append(NL);
                    llse.Append("Duration(sec)\t : "); llse.Append(NormStr(MyBinBVH.Length)); llse.Append(NL);
                    llse.Append("Frames\t\t : "); llse.Append(NormStr((float)mNumFrames)); llse.Append(NL);
                    if (mNumFrames * mFrameTime > 30f) // not more then 30sec animation time
                        mFrameTime = 30f / mNumFrames;
                    llse.Append("Frame Time\t : "); llse.Append(NormStr((float)mFrameTime)); llse.Append(NL);
                    llse.Append(NL);
                    llse.Append("Joint Name           Start Frame Values                  Last Frame Values"); llse.Append(NL);
                    llse.Append("----------    ------------------------------      ------------------------------"); llse.Append(NL);
                    for (i = 0; i < myTSBVH.mJoints.Length; ++i)
                    {
                        if (i == 0)    // position values for only the first joint (hip)
                        {
                            llse.Append("hip(Pos)    ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[1].mPos.X)); llse.Append(" ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[1].mPos.Y)); llse.Append(" ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[1].mPos.Z)); llse.Append("    ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[mNumFrames - 1].mPos.X)); llse.Append(" ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[mNumFrames - 1].mPos.Y)); llse.Append(" ");
                            llse.Append(NormStr(myTSBVH.mJoints[0].mPosRotKeys[mNumFrames - 1].mPos.Z)); llse.Append(NL);
                        }
                        uAnim = myTSBVH.mJoints[i].mName + "            ";
                        llse.Append(uAnim.Substring(0, 12));

                        string s = myTSBVH.mJoints[i].mOrder;
                        char[] c = s.ToCharArray();
                        float[] wRot = new float[3];
                        wRot[0] = myTSBVH.mJoints[i].mPosRotKeys[1].mRot.X;
                        wRot[1] = myTSBVH.mJoints[i].mPosRotKeys[1].mRot.Y;
                        wRot[2] = myTSBVH.mJoints[i].mPosRotKeys[1].mRot.Z;
                        // 
                        llse.Append(NormStr(wRot[c[0] - 'X']));
                        llse.Append(" ");
                        llse.Append(NormStr(wRot[c[1] - 'X']));
                        llse.Append(" ");
                        llse.Append(NormStr(wRot[c[2] - 'X']));
                        llse.Append("    ");
                        wRot[0] = myTSBVH.mJoints[i].mPosRotKeys[mNumFrames - 1].mRot.X;
                        wRot[1] = myTSBVH.mJoints[i].mPosRotKeys[mNumFrames - 1].mRot.Y;
                        wRot[2] = myTSBVH.mJoints[i].mPosRotKeys[mNumFrames - 1].mRot.Z;
                        // 
                        llse.Append(NormStr(wRot[c[0] - 'X']));
                        llse.Append(" ");
                        llse.Append(NormStr(wRot[c[1] - 'X']));
                        llse.Append(" ");
                        llse.Append(NormStr(wRot[c[2] - 'X']));
                        llse.Append(NL);
                    }



                    File.WriteAllText(xFileName, llse.ToString());

                    bBVH.Enabled = true;
                    bAnim.Enabled = true;
                    wAnim.Text = "";
                    labelSuccess.Text = "BVH file successfully created - " + wFileBVH.Text;

                }

                catch (System.Exception ex)
                {
//                    MessageBox.Show(aFiles[ii] + "\r\nNot Converted "+ ex.Message);
                }
            }

        }
        /* 
         * Utilities  
         * 
         */
        static Vector3 revQ(double p0, double p1, double p2, double p3, double e)
        {
            double dp0 = p0;
            double dp1 = p1;
            double dp2 = p2;
            double dp3 = p3;
            double de = e;
            double sint2 = 2.0 * (dp0 * dp2 + de * dp1 * dp3);
            float t1;
            float t2 = (float)Math.Asin(sint2);
            float t3;
            Vector3 ret;
            if (Math.Abs(sint2) > 0.9999995)
            {
                t1 = (float)Math.Atan2(dp1, dp0);
                if (sint2 < 0.0)
                {
                    t2 = (float)Math.Asin(-1.0);
                }
                else
                {
                    t2 = (float)Math.Asin(1.0);
                }
                t3 = 0;
            }
            else
            {
                t1 = (float)Math.Atan2(2.0 * (dp0 * dp1 - de * dp2 * dp3), 1.0 - 2.0 * (dp1 * dp1 + dp2 * dp2));
                t3 = (float)Math.Atan2(2.0 * (dp0 * dp3 - de * dp1 * dp2), 1.0 - 2.0 * (dp2 * dp2 + dp3 * dp3));
            }
            ret.X = t1;
            ret.Y = t2;
            ret.Z = t3;
            ret = ret * Utils.RAD_TO_DEG;
            return ret;
        }
        static Vector3 revMayaQ(Quaternion q, Order order)
        {
            Vector3 ret;
            Vector3 tmp;
            float t1 = 0;
            float t2 = 0;
            float t3 = 0;
            switch (order)
            {
                case Order.ZYX:
                    tmp = revQ(q.W, q.Z, q.Y, q.X, -1);
                    t1 = tmp.Z;
                    t2 = tmp.Y;
                    t3 = tmp.X;
                    break;
                case Order.XZY:
                    tmp = revQ(q.W, q.X, q.Z, q.Y, -1);
                    t1 = tmp.X;
                    t2 = tmp.Z;
                    t3 = tmp.Y;
                    break;
                case Order.YZX:
                    tmp = revQ(q.W, q.Y, q.Z, q.X, 1);
                    t1 = tmp.Z;
                    t2 = tmp.X;
                    t3 = tmp.Y;
                    break;
                case Order.XYZ:
                    tmp = revQ(q.W, q.X, q.Y, q.Z, 1);
                    t1 = tmp.X;
                    t2 = tmp.Y;
                    t3 = tmp.Z;
                    break;
                default:
                    // ERROR
                    break;
            }
            ret.X = t1;
            ret.Y = t2;
            ret.Z = t3;
            return ret;
        }

        string NormStr(float nz)
        {
            int FirstChr;
            string sWithSpace;
            string sp = "          ";
            float wfl = (float)Math.Round(nz, 6);
            string sOrig = wfl.ToString("#0.000000", new CultureInfo("en-US"));

            FirstChr = sOrig.IndexOf("."); //
            if (FirstChr < 4)
                sWithSpace = sp.Substring(0, 4 - FirstChr) + sOrig;
            else
                sWithSpace = sOrig;
            string sRet = sWithSpace.Substring(0, 10);
            return sRet;
        }
        // linear interpolation
        public Quaternion lerp(float t, Quaternion p, Quaternion q)
        {
            Quaternion r;
            float inv_t;
            Quaternion qn;

            inv_t = 1f - t;

            r.X = t * q.X + (inv_t * p.X);
            r.Y = t * q.Y + (inv_t * p.Y);
            r.Z = t * q.Z + (inv_t * p.Z);
            r.W = t * q.W + (inv_t * p.W);
            qn = Quaternion.Normalize(r);
            return qn;
        }
        // spherical linear interpolation
        Quaternion slerp(float u, Quaternion a, Quaternion b)
        {
            // cosine theta = dot product of a and b
            float cos_t = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

            // if b is on opposite hemisphere from a, use -a instead
            bool bflip;
            if (cos_t < 0.0f)
            {
                cos_t = -cos_t;
                bflip = true;
            }
            else
                bflip = true;

            // if B is (within precision limits) the same as A,
            // just linear interpolate between A and B.
            float alpha;	// interpolant
            float beta;		// 1 - interpolant
            if (1.0f - cos_t < 0.00001f)
            {
                beta = 1.0f - u;
                alpha = u;
            }
            else
            {
                float theta = (float)Math.Cos(cos_t);
                float sin_t = (float)Math.Sin(theta);
                beta = (float)Math.Sin(theta - u * theta) / sin_t;
                alpha = (float)Math.Sin(u * theta) / sin_t;
            }

            if (bflip)
                beta = -beta;

            // interpolate
            Quaternion ret;
            ret.X = beta * a.X + alpha * b.X;
            ret.Y = beta * a.Y + alpha * b.Y;
            ret.Z = beta * a.Z + alpha * b.Z;
            ret.W = beta * a.W + alpha * b.W;

            return ret;
        }

        // lerp whenever possible
        Quaternion nlerp(float t, Quaternion a, Quaternion b)
        {
            if ((a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W) < 0.0f)
            {
                return slerp(t, a, b);
            }
            else
            {
                return lerp(t, a, b);
            }
        }

        public string HandPose(uint Hp)
        {
            string ret = "";
            switch (Hp)
            {
                case 0:
                    ret = "Spread";
                    break;
                case 1:
                    ret = "Relaxed";
                    break;
                case 2:
                    ret = "Point_Both";
                    break;
                case 3:
                    ret = "Fist";
                    break;
                case 4:
                    ret = "Relaxed_Left";
                    break;
                case 5:
                    ret = "Point_Left";
                    break;
                case 6:
                    ret = "Fist_Left";
                    break;
                case 7:
                    ret = "Relaxed_Right";
                    break;
                case 8:
                    ret = "Point_Right";
                    break;
                case 9:
                    ret = "Fist_Right";
                    break;
                case 10:
                    ret = "Salute_Right";
                    break;
                case 11:
                    ret = "Typing";
                    break;
                case 12:
                    ret = "Peace_Right";
                    break;
            }
            return ret;
        }


    }
}