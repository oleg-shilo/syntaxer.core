using System;

// using System.Windows.Forms;

class Script
{
    [STAThread]
    static public void Main3(string[] args)
    {
        // MessageBox.Show("Just a test!");
        Console.WriteLine("Just a test!");

        for (int i = 0; i < args.Length; i++)
        {
            Console.WriteLine(args[i]);
        }
    }

    int Index { get; set; }
}

class Script2
{
    static public void Main2(string[] args)
    {
        // MessageBox.Show("Just a test!");

        for (int i = 0; i < args.Length; i++)
        {
            Console.WriteLine(args[i]);
        }
    }

    int Index { get; set; }
}