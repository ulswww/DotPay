/*
Template Name: Color Dotpay.Admin - Responsive Dotpay.Admin Dashboard Template build with Twitter Bootstrap 3.3.2
Version: 1.6.0
Author: Sean Ngu
Website: http://www.seantheme.com/color-admin-v1.6/admin/
*/

var handleDataTableDefault = function() {
	"use strict";
    
    if ($('#data-table').length !== 0) {
        $('#data-table').DataTable();
    }
};

var TableManageDefault = function () {
	"use strict";
    return {
        //main function
        init: function () {
            $.getScript('assets/plugins/DataTables/js/jquery.dataTables.js').done(function() {
                handleDataTableDefault();
            });
        }
    };
}();