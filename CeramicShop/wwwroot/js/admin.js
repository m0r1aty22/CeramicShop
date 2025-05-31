// Admin JavaScript
document.addEventListener("DOMContentLoaded", () => {
    // Toggle sidebar
    var sidebarToggle = document.getElementById("sidebarToggle");
    if (sidebarToggle) {
        sidebarToggle.addEventListener("click", (e) => {
            e.preventDefault();
            document.body.classList.toggle("sb-sidenav-toggled");
            // Lưu trạng thái sidebar vào localStorage nếu muốn
            localStorage.setItem("sb|sidebar-toggle", document.body.classList.contains("sb-sidenav-toggled"));
        });

        // Khôi phục trạng thái sidebar từ localStorage nếu có
        if (localStorage.getItem("sb|sidebar-toggle") === "true") {
            document.body.classList.add("sb-sidenav-toggled");
        }
    }

    // Set active nav item based on current URL
    var currentUrl = window.location.pathname
    var navLinks = document.querySelectorAll(".nav-link")
    navLinks.forEach((link) => {
        var href = link.getAttribute("href")
        if (href && currentUrl.includes(href) && href !== "/Admin") {
            link.classList.add("active")
        } else if (currentUrl === "/Admin" && href === "/Admin") {
            link.classList.add("active")
        }
    })

    // Initialize tooltips
    var $ = window.jQuery // Declare jQuery
    if (typeof $ !== "undefined") {
        // Check if jQuery is loaded
        if (typeof jQuery !== "undefined") {
            // $ = jQuery; // Assign jQuery to $ - No need to reassign as it's already assigned above
            $('[data-toggle="tooltip"]').tooltip()

            // Initialize popovers
            $('[data-toggle="popover"]').popover()
        } else {
            console.error("jQuery is not loaded. Tooltips and popovers will not be initialized.")
        }
    } else {
        console.error("jQuery is not loaded. Tooltips and popovers will not be initialized.")
    }

    // File input preview for product images
    var fileInput = document.getElementById("productImages")
    var previewContainer = document.getElementById("imagePreviewContainer")

    if (fileInput && previewContainer) {
        fileInput.addEventListener("change", function () {
            previewContainer.innerHTML = ""

            if (this.files) {
                for (var i = 0; i < this.files.length; i++) {
                    var file = this.files[i]
                    if (!file.type.match("image.*")) {
                        continue
                    }

                    var reader = new FileReader()
                    reader.onload = ((theFile, index) => (e) => {
                        var div = document.createElement("div")
                        div.className = "image-preview-item"
                        div.innerHTML = [
                            '<img src="',
                            e.target.result,
                            '" title="',
                            escape(theFile.name),
                            '"/>',
                            index === 0 ? '<span class="badge badge-success">Ảnh chính</span>' : "",
                        ].join("")

                        previewContainer.appendChild(div)
                    })(file, i)

                    reader.readAsDataURL(file)
                }
            }
        })
    }

    // Category change event for subcategory dropdown
    var categorySelect = document.getElementById("categorySelect")
    var subCategorySelect = document.getElementById("SubCategoryID")

    //if (categorySelect && subCategorySelect) {
    //    categorySelect.addEventListener("change", function () {
    //        var categoryId = this.value
    //        if (categoryId) {
    //            fetch("/AdminProduct/GetSubCategories?categoryId=" + categoryId)
    //                .then((response) => response.json())
    //                .then((data) => {
    //                    subCategorySelect.innerHTML = '<option value="">-- Chọn danh mục con --</option>'
    //                    data.forEach((item) => {
    //                        var option = document.createElement("option")
    //                        option.value = item.id
    //                        option.textContent = item.name
    //                        subCategorySelect.appendChild(option)
    //                    })
    //                })
    //                .catch((error) => console.error("Error:", error))
    //        } else {
    //            subCategorySelect.innerHTML = '<option value="">-- Chọn danh mục con --</option>'
    //        }
    //    })
    //}

    // Confirm delete
    var deleteButtons = document.querySelectorAll(".btn-delete")
    deleteButtons.forEach((button) => {
        button.addEventListener("click", (e) => {
            if (!confirm("Bạn có chắc chắn muốn xóa mục này?")) {
                e.preventDefault()
            }
        })
    })

    // Auto-hide alerts after 5 seconds
    $ = window.jQuery // Declare jQuery
    if (typeof $ !== "undefined") {
        if (typeof jQuery !== "undefined") {
            // $ = jQuery; // Assign jQuery to $ - No need to reassign as it's already assigned above
            setTimeout(() => {
                $(".alert-dismissible").alert("close")
            }, 5000)
        } else {
            console.warn("jQuery is not loaded. Auto-hide alerts will not work.")
        }
    } else {
        console.warn("jQuery is not loaded. Auto-hide alerts will not work.")
    }
})
