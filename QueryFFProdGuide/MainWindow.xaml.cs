using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace QueryFFProdGuide
{
    /* For Family Farm players. Reads https://farm-us.funplusgame.com/index.php/facebook/showStore/?lang=am to see which items are required for a product. 
     * Then provides a way for the user to enter a query and get a solution based on what was read.
     */

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public class Item
    {
        /* Items that are used to make a product. The name of an item may be the name of a product.
         * For example, a tuna taco, a cucumber and a tomato are used to make a tuna taco salad.
         * But additional items are required to make a tuna taco, so the tuna taco is also a product.
         */
        public Item(string name, int quantity = 1)
        {
            Name = name;
            Quantity = quantity;
        }

        // Name that's read from the web page.
        public string Name { get; set; }

        // Quantity that's needed to make the product.
        public int Quantity { get; set; }
    }

    public class Product
    {
        // Items that can be queried.
        public Product(string name, bool any = false)
        {
            Name = name;
            Any = any;
            SourceList = new List<string>();
            ItemList = new List<Item>();
        }

        // Name that's read from the web page.
        public string Name { get; set; }

        /* If true, you can use any of the items in ItemList, as well as the animal or machine, to get the product. Applies to nectar and various types of honey. 
         * If false, you need all of the items in the ItemList.
         */
        public bool Any { get; set; }

        // Animals or machines that can be used to make the product.
        public List<string> SourceList { get; set; }

        // Items to feed to the animal or machine to get the product.
        public List<Item> ItemList { get; set; }
    }

    public partial class MainWindow : Window
    {
        enum TableType
        {
            None,
            ProductLast,
            ProductFirst
        }

        public Dictionary<string, Product> products;

        public MainWindow()
        {
            InitializeComponent();
            OutputTextBox.AppendText("Please wait while I gather the items off of the web page................\r\n");
            products = new Dictionary<string, Product>();

            /* Provides a way to call an asychronous method from a main function without any complaints from the compiler.
             * Normally, you would make the calling function an asynchronous method, but I can't do that here, because
             * it's a main function. Note that await capability isn't available here.
             */
            ParseHtmlAsync().ContinueWith(task => { }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Reads the web page and fills the products dictionary. When finished, the buttons and the query entry window will be enabled.
        private async Task ParseHtmlAsync()
        {
            // Declare an HttpClient object, and increase the buffer size. The default buffer size is 65,536.  
            HttpClient client =
                new HttpClient() { MaxResponseContentBufferSize = 100000000 };

            // Read the web page.
            var content = await client.GetStringAsync("https://farm-us.funplusgame.com/index.php/facebook/showStore/?lang=am");

            // Traverse the HTML.
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var htmlBody = htmlDoc.DocumentNode.SelectSingleNode("//body");

            HtmlNodeCollection tableNodes = htmlBody.ChildNodes;

            var tt = TableType.None;

            foreach (var tableNode in tableNodes)
            {
                if (tableNode.Name == "table")
                {
                    tt += 1;

                    HtmlNodeCollection trNodes = tableNode.ChildNodes;

                    if (tt == TableType.ProductLast)
                    {
                        ParseTableProductLast(tableNode.ChildNodes);
                    }
                    else
                    {
                        ParseTableProductFirst(tableNode.ChildNodes);
                    }
                }
            }

            OutputTextBox.AppendText("Ready\r\n");
            QueryTextBox.IsEnabled = true;
            SubmitQueryButton.IsEnabled = true;
            ClearQueryButton.IsEnabled = true;
            ClearAllButton.IsEnabled = true;
        }

        /* Handles 2 scenarios.
         * 
         * Scenario 1: Table contains 3 or 4 columns. The first column contains the animal or machine to get the product from. The second contains the item needed to make the 
         * product. If 4 columns, the third column contains 1 or more additional items that are constant. The last column contains the final product. There may be 
         * more than one item in the second and final columns, but they will always be the same number of items. The way it works is, the item in the second column, 
         * plus the additional items, if any, makes the corresponding item in the last column. So to make the 5th product in the last column, for example, you would 
         * use the 5th item in the second column. Then if there are 4 columns, you would also use the items in the third column.
         * 
         * Scenario 2: Table contains 3 columns. The first column contains the animal or machine to get the product from. The second contains a list of items that need to be
         * pollinated. The third column contains the product. You can have any item in the second column to get the item in the third column.
         */
        private void ParseTableProductLast(in HtmlNodeCollection trNodes)
        {
            const string ss1 = "&nbsp;";    // Separator.
            const string ss2 = "\r\n";      // ss1 will be followed by an item name or this. If this, go past it and search for ss1 again.

            List<Item> itemList;
            int i1, i2;
            bool addingItem;
            string key, source;

            var itemList1 = new List<Item>();
            var itemList2 = new List<Item>();
            var productList = new List<string>();

            foreach (var trNode in trNodes)
            {
                source = "";

                foreach (var tdNode in trNode.ChildNodes)
                {
                    if (tdNode.Name == "td")
                    {
                        if (source.Length == 0)
                        {
                            // First column. Get the name of the animal or machine that will produce the item.
                            source = tdNode.InnerText;
                        }

                        itemList = itemList1;

                        if (itemList1.Count == 0)
                        {
                            // Second column.
                            itemList = itemList1;
                            addingItem = true;
                        }
                        else if (itemList2.Count == 0)
                        {
                            // Third or final column.
                            itemList = itemList2;
                            addingItem = true;
                        }
                        else
                        {
                            // Fourth column.
                            addingItem = false;
                        }

                        i1 = 0;

                        // True if we're in the second through final columns.
                        while ((i2 = tdNode.InnerText.Substring(i1).IndexOf(ss1)) != -1)
                        {
                            // Go past the separator.
                            i1 += i2 + ss1.Length;

                            // If true, i1 is at the start of the item name.
                            if (!tdNode.InnerText.Substring(i1).StartsWith(ss2))
                            {
                                // If true, there are more items or products in the list.
                                if ((i2 = tdNode.InnerText.Substring(i1).IndexOf(ss2)) != -1)
                                {
                                    // In columns 2 or 3.
                                    if (addingItem)
                                    {
                                        var item = new Item(name: tdNode.InnerText.Substring(i1, i2));
                                        itemList.Add(item);
                                    }
                                    // In column 4. Get the name of the product.
                                    else
                                    {
                                        productList.Add(tdNode.InnerText.Substring(i1, i2));
                                    }

                                    // Go past the item we just added, plus the separator.
                                    i1 += i2 + ss2.Length;
                                }
                                else
                                {
                                    // Last item or product. Add it, then break.
                                    if (addingItem)
                                    {
                                        var item = new Item(name: tdNode.InnerText.Substring(i1));
                                        itemList.Add(item);
                                    }
                                    else
                                    {
                                        productList.Add(tdNode.InnerText.Substring(i1));
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // This is a 3-column table. Move the contents of itemList2 to productList.
                if (itemList2.Count != 0 && productList.Count == 0)
                {
                    foreach (var item in itemList2)
                    {
                        productList.Add(item.Name);
                    }
                    itemList2.Clear();
                }

                if (productList.Count != 0)
                {
                    // Scenario 1.
                    if (itemList1.Count == productList.Count)
                    {
                        for (var i = 0; i < itemList1.Count; i++)
                        {
                            key = NameToKey(productList[i]);

                            if (products.ContainsKey(key))
                            {
                                /* We already have this product in the dictionary, so add source
                                 * to the list of animals/machines that are used to make it.
                                 */
                                products[key].SourceList.Add(source);
                            }
                            else
                            {
                                var product = new Product(productList[i]);

                                // Add the primary item to the list of items used to make this product.
                                product.ItemList.Add(itemList1[i]);

                                // Add the constant items, if any, to the same list.
                                for (var j = 0; j < itemList2.Count; j++)
                                {
                                    product.ItemList.Add(itemList2[j]);
                                }

                                // Now add the animal or machine that's used to make this product
                                product.SourceList.Add(source);

                                // ...and add this product to the dictionary.
                                products.Add(key, product);
                            }
                        }
                    }
                    // Scenario 2.
                    else
                    {
                        // There had better be exactly one product.
                        if (productList.Count != 1)
                        {
                            Console.WriteLine($"Unexpected: itemList1 has {itemList1.Count} Products, and productList has {productList.Count} Products");
                            OutputTextBox.AppendText($"Unexpected: itemList1 has {itemList1.Count} Products, and productList has {productList.Count} Products\r\n");
                        }
                        else
                        {
                            key = NameToKey(productList[0]);

                            if (products.ContainsKey(key))
                            {
                                /* We already have this product in the dictionary, so add source
                                 * to the list of animals/machines that are used to make it.
                                 */
                                products[key].SourceList.Add(source);
                            }
                            else
                            {
                                // Note that we are setting the Any property to true here.
                                var product = new Product(name: productList[0], any: true);

                                // Add the animal or machine that's used to make this product.
                                product.SourceList.Add(source);

                                // Add all of the items that can be used
                                for (var i = 0; i < itemList1.Count; i++)
                                {
                                    product.ItemList.Add(itemList1[i]);
                                }

                                // ...then add this product to the dictionary.
                                products.Add(key, product);
                            }
                        }
                    }
                }

                // Clear the lists for the next iteration.
                itemList1.Clear();
                itemList2.Clear();
                productList.Clear();
            }
        }

        /* Handles a table where the product is listed first. There are 2 columns. The first column contains the name of the product, 
         * and the second column contains a list of the items that are needed to make the product. But in this case, the quantity of
         * each item is also listed.
         */
        private void ParseTableProductFirst(in HtmlNodeCollection trNodes)
        {
            const string ss1 = "&nbsp;";    // Separator.
            const string ss2 = "\r\n";      // ss1 will be followed by an item name or this. If this, go past it and search for ss1 again.
            const string ss3 = " * ";       // Separates the name of the item from its quantity.

            Item item;
            int i1, i2, i3;
            string name;

            var itemList = new List<Item>();

            foreach (var trNode in trNodes)
            {
                name = "";

                foreach (var tdNode in trNode.ChildNodes)
                {
                    if (tdNode.Name == "td")
                    {
                        if (name.Length == 0)
                        {
                            // Get the item name.
                            if ((i1 = tdNode.InnerText.IndexOf(" :")) != -1)
                            {
                                name = tdNode.InnerText.Substring(0, i1);
                            }
                            else
                            {
                                name = tdNode.InnerText;
                            }
                        }

                        i1 = 0;

                        // True only for the second column.
                        while ((i2 = tdNode.InnerText.Substring(i1).IndexOf(ss1)) != -1)
                        {
                            // Go past the separator.
                            i1 += i2 + ss1.Length;

                            // If true, i1 is at the start of the item name.
                            if (!tdNode.InnerText.Substring(i1).StartsWith(ss2))
                            {
                                // There are more items in the list.
                                if ((i2 = tdNode.InnerText.Substring(i1).IndexOf(ss2)) != -1)
                                {
                                    if ((i3 = tdNode.InnerText.Substring(i1, i2).IndexOf(ss3)) != -1)
                                    {
                                        // Use the given quantity.
                                        item = new Item(name: tdNode.InnerText.Substring(i1, i3), quantity: int.Parse(tdNode.InnerText.Substring(i1 + i3 + ss3.Length, i2)));
                                    }
                                    else
                                    {
                                        // Use the default quantity of 1.
                                        item = new Item(name: tdNode.InnerText.Substring(i1, i2));
                                    }

                                    // Add this item to the list.
                                    itemList.Add(item);

                                    // Go past the item we just added, plus the separator.
                                    i1 += i2 + ss2.Length;
                                }
                                else
                                // This is the end of the list.
                                {
                                    if ((i3 = tdNode.InnerText.Substring(i1, i2).IndexOf(ss3)) != -1)
                                    {
                                        // Use the given quantity.
                                        item = new Item(name: tdNode.InnerText.Substring(i1, i3), quantity: int.Parse(tdNode.InnerText.Substring(i1 + i3 + ss3.Length)));
                                    }
                                    else
                                    {
                                        // Use the default quantity of 1.
                                        item = new Item(name: tdNode.InnerText.Substring(i1));
                                    }

                                    // Add this item to the list
                                    itemList.Add(item);

                                    // ...and break out of the loop.
                                    break;
                                }
                            }
                        }
                    }
                }

                if (name.Length != 0 && itemList.Count != 0)
                {
                    // Add this product.
                    var product = new Product(name);

                    // Copy itemList to the product's ItemList.
                    for (var i = 0; i < itemList.Count; i++)
                    {
                        product.ItemList.Add(itemList[i]);
                    }

                    // Add this product to the dictionary.
                    products.Add(NameToKey(name), product);
                }

                // Now clear ItemList for the next iteration.
                itemList.Clear();
            }
        }

        // Process the query that was given.
        private void SubmitQueryButton_Click(object sender, RoutedEventArgs e)
        {
            if (OutputTextBox.Text.Length != 0)
            {
                // To separate output between queries.
                OutputTextBox.AppendText("\r\n");
            }

            if (QueryTextBox.Text.Length == 0)
            {
                OutputTextBox.AppendText("You must enter a query\r\n");
                return;
            }

            ParseQuery(QueryTextBox.Text, out int quantity, out string key);

            if (products.ContainsKey(key))
            {
                var product = products[key];

                /* Output the name and quantity of the item queried, so the user can
                 * recall what was queried previously.
                 */
                OutputTextBox.AppendText($"To get {quantity} {product.Name}, you'll need ");

                // Output the items needed.
                if (product.Any && product.ItemList.Count > 1)
                {
                    OutputTextBox.AppendText("any of ");
                }

                foreach (var item in WriteList(product.ItemList))
                {
                    if (product.Any)
                    {
                        OutputTextBox.AppendText($"{item.Name}");
                    }
                    else
                    {
                        OutputTextBox.AppendText($"{item.Quantity * quantity} {item.Name}");
                    }
                }

                OutputTextBox.AppendText("\r\n");

                // Output the thing that's being used to collect the item, if available.
                if (product.SourceList.Count == 1)
                {
                    OutputTextBox.AppendText($"You'll also need {product.SourceList[0]} to collect {product.Name}\r\n");
                }
                else if (product.SourceList.Count != 0)
                {
                    OutputTextBox.AppendText("You'll also need any of ");

                    foreach (var item in WriteList(product.SourceList))
                    {
                        OutputTextBox.AppendText($"{item}");
                    }

                    OutputTextBox.AppendText($" to collect {product.Name}\r\n");
                }
            }
            // Item not available.
            else
            {
                OutputTextBox.AppendText($"Can't find {QueryTextBox.Text}\r\n");
            }

            /* Causes the box to scroll down to the output from the last query. 
             * Needed because OutputTextBox is readonly.
             */
            OutputTextBox.ScrollToEnd();
        }

        // Ouputs a comma-separated list.
        private IEnumerable<T> WriteList<T>(List<T> list)
        {
            var firstTime = true;

            foreach (var item in list)
            {
                if (firstTime)
                {
                    firstTime = false;
                }
                else
                {
                    OutputTextBox.AppendText(", ");
                }
                yield return item;
            }
        }

        private void ClearQueryButton_Click(object sender, RoutedEventArgs e)
        {
            QueryTextBox.Clear();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            QueryTextBox.Clear();
            OutputTextBox.Clear();
        }

        /* Parse the query that was given. Expects an optional quantity and a product name.
         * If no quantity is given, assume a quantity of 1.
         */
        private void ParseQuery(in string query, out int quantity, out string key)
        {
            int i;

            // Removes leading and trailing whitespace.
            key = query.Trim();

            quantity = 1;

            if ((i = key.IndexOf(' ')) >= 0)
            {
                if (int.TryParse(key.Substring(0, i), out quantity))
                {
                    // Use the given quantity.
                    key = key.Substring(i + 1, key.Length - (i + 1));
                }
                else
                {
                    // Use the default quantity.
                    quantity = 1;
                }
            }

            // Get the key of the item.
            key = NameToKey(key);
        }

        /* Given an item's name (from the production guide), return the key.
         * Making the key lower-case so the user doesn't have to remember
         * which words are capitalized when entering the product name
         * in the query. Making it singular in case the user has a tendency
         * to pluralize the item in some cases (it makes more sense to say
         * "2 salads" then "2 salad", for instance). Also, some items are
         * already listed as a plural (i.e. Christmas stockings), so the
         * reverse also applies.
         */
        private string NameToKey(in string name)
        {
            return Singular(name.ToLower());
        }

        // If an item name appears to be plural, convert it to singular.
        private string Singular(in string in_string)
        {
            string[] plurals = { "ies", "es", "s" };

            char[] vowels = { 'a', 'e', 'i', 'o', 'u' };

            // Example: Batteries
            if (in_string.EndsWith(plurals[0]))
            {
                return in_string.Substring(0, in_string.Length - plurals[0].Length) + "y";
            }
            // Example: Buffaloes
            else if (in_string.EndsWith(plurals[1]) && Array.IndexOf(vowels, in_string[in_string.Length - plurals[1].Length - 1]) >= 0)
            {
                return in_string.Substring(0, in_string.Length - plurals[1].Length);
            }
            // Example: Buffalos, Names. Not perfect as Sarcophagus will return Sarcophagu.
            else if (in_string.EndsWith(plurals[2]))
            {
                return in_string.Substring(0, in_string.Length - plurals[2].Length);
            }
            // Assume it's already singular.
            else
            {
                return in_string;
            }
        }
    }
}
