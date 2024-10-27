using System;

[Serializable]
public class ArrayLayout
{
	[Serializable]
	public struct rowData
	{
		public bool[] row;
	}

	public rowData[] rows = new rowData[8]; //creates a grid with a Y of 8, ultimately controlled by the CustPropertyDrawer.cs

}