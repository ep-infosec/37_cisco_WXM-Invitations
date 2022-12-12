# DISPATCHER WEB PORTAL

This tool will help you configure the setup required to send invites using Webex Experience Management.

## Deployment Configuration

The file `js/main.js` has a constant `BASE_URL` that defines the `baseURL`. This is the url where the Partner Hosted Dispatcher(backend) is deployed. On new deployment, get update this with the url where the backend application runs.

## Instructions to add new vendor

A new vendor can be added to the portal manually. Follow the steps below.

### Create New Vendor Modal Form

In the file `config-file.html`, find the commented section `Add new vendor in a modal here`. Add a new form with pop up modal id. Add the required fields as per the API requirements as shown in the example.

    <div class="form__group">
        <label for="vendorAPIKey" class="form__label">Api Key</label>
        <input type="text" id="vendorAPIKey" name="ApiKey" value="" class="form__field" required
            placeholder="Enter Api Key">
    </div>

Add a cancel and Save button within this form like other.

### Add a selector input mapping

Map your input fields with the id that is assigned to it in the form. This has to be done to convert the data to a JSON while saving/editing. Ensure the input elements has name that matches with the key of JSON data that needs to be saved. In the above example, it follows

data = {
ApiKey: '\*\*\*\*'
}

### Create Read Only View

Once the Modal form is designed with the required form fields, we now create the read only view. Add the new vendor name in the dropdown. Find the block commented as `Add new Email Vendor name for dropdown selection`. Add a new entry for your vendor to appear in the selection. Add accordingly the SMS vendor name to SMS vendor dropdown.

Now add the block to show the values under `New Vendor Read Only View`. Add the data to be displayed as shown in the example.

    <h4> URL </h4>
    <span id="newVendorURL"></span>

### Load data on selection

Now that the Read only form is ready, add a function to load the vendor specific data from the backend similar to sparkpost.

Under function `onEmailSelectChange()` in `main.js`, add a condition to check if the selected value matches the newly added vendor id in the dropdown. If so then call the function to get data from the backend. This new function will get the data via ajax API call and set the data to Read only form elements we created in previous step. Refer `getSparkPostData` for help.

### Open the modal pop up

Adding the vendorName to the drop down will automatically wire the edit button. On clicking the edit button we will need to show the modal pop up for user to edit.
In `config-file.html` add the new vendor related code below `Add the new vendor modal with right ID`

Get the cancel button id from the form that we just added. Create an onclick handler to clear the form elements within the form.

Add the callback to open the form pop up like shown below.

    if (document.getElementById('getVendorSms').value == "newVendorModalId") {
        getNewVendorData();
        $("body").css({"overflow": "hidden"});
        newVendorModal.style.display = "block";
    }

### Save data from the form

Add a callback to the save button within the new modal form. This new function will validate the data in the form. Collect the data. Serialize it to a JSON and make API call to the backend.
Utility functions like `emailFormat`, `required` and `serialize` will come in handy to make these calls.

Use `vendorEmailUpdateAPI()` function for reference and ensure the data is posted to backend in the required format.

### Deployment Check

Before deployment, switch the `isProduction` flag to `true`. This will freeze the baseURL to the one configured.
