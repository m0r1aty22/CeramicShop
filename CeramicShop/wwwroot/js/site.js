// Global JavaScript functions

// ===== GLOBAL VARIABLES =====
let isDebugMode = false;

// ===== DOCUMENT READY =====
$(document).ready(() => {
    console.log("Document ready - Site.js loaded");

    if (typeof AOS !== "undefined") {
        AOS.init({ duration: 800, once: true });
    }

    if (typeof bootstrap !== "undefined") {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map((tooltipTriggerEl) => new bootstrap.Tooltip(tooltipTriggerEl));
        const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
        popoverTriggerList.map((popoverTriggerEl) => new bootstrap.Popover(popoverTriggerEl));
    }

    setupBackToTop();
    setupStickyHeader();
    setupMobileMenu();
    setupTimelineAnimation();
    setupSearchAutocomplete();
    getCartCount();
    initializeWishlistButtons(); // QUAN TRỌNG: Gọi hàm khởi tạo wishlist
    setupEventHandlers(); // Hàm này sẽ KHÔNG còn xử lý wishlist nữa

    if ($("#product-details-page").length) {
        initializeProductDetailsPage(); // Hàm này sẽ KHÔNG còn gọi checkWishlistStatus AJAX nữa
    }
    if ($("#wishlist-page").length) {
        // checkWishlistItems(); // Hàm này có thể không cần thiết nếu server render đúng
    }
    $("#debug-mode-toggle").on("click", () => { toggleDebugMode(); });
    setupAccountFunctionality();
    setupCartFunctionality();
    if (isDebugMode) { setupDebugListeners(); }
});

// ===== SETUP FUNCTIONS =====

// Back to top button
function setupBackToTop() {
    var backToTopButton = $('<button class="back-to-top"><i class="fas fa-arrow-up"></i></button>');
    $("body").append(backToTopButton);
    $(window).scroll(function () {
        if ($(this).scrollTop() > 300) { $(".back-to-top").fadeIn(); }
        else { $(".back-to-top").fadeOut(); }
    });
    $(".back-to-top").click(() => { $("html, body").animate({ scrollTop: 0 }, 500); return false; });
}

// Sticky header
function setupStickyHeader() {
    var header = $(".main-header");
    var headerOffset = header.offset() ? header.offset().top : 0;
    $(window).scroll(function () {
        if ($(this).scrollTop() > headerOffset) {
            header.addClass("sticky-header");
            $("body").css("padding-top", header.outerHeight());
        } else {
            header.removeClass("sticky-header");
            $("body").css("padding-top", 0);
        }
    });
}

// Mobile menu
function setupMobileMenu() {
    $(".mobile-menu-toggle").click(() => {
        $(".mobile-menu").toggleClass("open");
        $("body").toggleClass("menu-open");
    });
    $(document).click((event) => {
        if (!$(event.target).closest(".mobile-menu, .mobile-menu-toggle").length) {
            $(".mobile-menu").removeClass("open");
            $("body").removeClass("menu-open");
        }
    });
}

// Timeline animation
function setupTimelineAnimation() {
    function animateTimeline() {
        $(".timeline-item").each(function (index) {
            var item = $(this)
            setTimeout(() => {
                item.addClass("animate")
            }, 300 * index)
        })
    }

    // Run timeline animation if it exists on the page
    if ($(".order-timeline").length) {
        animateTimeline()
    }
}

// Search autocomplete
function setupSearchAutocomplete() {
    if ($("#searchInput").length && $.fn.autocomplete) {
        $("#searchInput")
            .autocomplete({
                source: (request, response) => {
                    $.ajax({
                        url: "/Product/SearchSuggestions",
                        data: { term: request.term },
                        success: (data) => {
                            response(data)
                        },
                    })
                },
                minLength: 2,
                select: (event, ui) => {
                    window.location.href = "/Product/Details/" + ui.item.id
                    return false
                },
            })
            .autocomplete("instance")._renderItem = (ul, item) =>
                $("<li>")
                    .append(
                        '<div class="search-suggestion-item">' +
                        '<img src="' +
                        item.image +
                        '" class="search-suggestion-image" alt="' +
                        item.label +
                        '">' +
                        "<div>" +
                        '<div class="search-suggestion-name">' +
                        item.label +
                        "</div>" +
                        '<div class="search-suggestion-price">' +
                        item.price.toLocaleString("vi-VN") +
                        " VNĐ</div>" +
                        "</div>" +
                        "</div>",
                    )
                    .appendTo(ul)
    }
}

// Get cart count
function getCartCount() {
    $.ajax({
        url: "/Cart/GetCartCount",
        type: "GET",
        success: (data) => {
            if (data && typeof data.count !== "undefined") {
                $(".cart-count").text(data.count)
            } else {
                $(".cart-count").text("0")
            }
        },
        error: (jqXHR, textStatus, errorThrown) => {
            console.error("Error fetching cart count: " + textStatus, errorThrown)
            $(".cart-count").text("0")
        },
    })
}

// Setup event handlers
function setupEventHandlers() {
    // Add to cart functionality (Nút có class .add-to-cart sẽ được xử lý ở đây)
    $(document).on("click", ".add-to-cart", function () {
        var productId = $(this).data("product-id");
        var quantity = 1;
        var productName = $(this).data("product-name") || "Sản phẩm";
        if ($("#quantity").length && $(this).attr('id') === 'addToCartBtnDetails') { // Chỉ lấy quantity từ input nếu là nút trên trang details
            quantity = Number.parseInt($("#quantity").val()) || 1;
        }
        addToCart(productId, quantity, productName); // Gọi hàm addToCart chung
    });

    // Cancel order functionality
    setupCancelOrderHandler()

    // Product tabs
    $(document).on("click", ".product-tab-link", function () {
        $(".product-tab-link").removeClass("active")
        $(this).addClass("active")

        var tabId = $(this).data("tab")
        $(".product-tab-content").removeClass("active")
        $("#" + tabId).addClass("active")
    })

    // Rating stars
    setupRatingStars()

    // Share buttons
    setupShareButtons()
}

// ===== NOTIFICATION FUNCTIONS =====

// Show notification
window.showNotification = (message, type = "info", title = "Thông báo") => { /* ... Giữ nguyên code của bạn, có thể tăng delay ... */
    // Check if Bootstrap is loaded
    if (typeof bootstrap === "undefined") {
        console.error("Bootstrap is not loaded. Please include Bootstrap in your project.")
        alert(message)
        return
    }
    var toast = $(
        '<div class="toast" role="alert" aria-live="assertive" aria-atomic="true">' +
        '<div class="toast-header bg-' + type + ' text-white">' +
        '<strong class="me-auto">' + title + "</strong>" +
        '<button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>' +
        "</div>" +
        '<div class="toast-body">' + message + "</div>" +
        "</div>",
    );
    var container = $("#notification-container");
    if (container.length === 0) {
        container = $('<div id="notification-container" class="position-fixed top-0 end-0 p-3" style="z-index: 1150; margin-top: 70px;"></div>'); // Tăng z-index
        $("body").append(container);
    }
    container.append(toast);
    var bsToast = new bootstrap.Toast(toast, { autohide: true, delay: 3000 }); // 3 giây
    toast.on('hidden.bs.toast', function () { $(this).remove(); });
    bsToast.show();
    return bsToast;
};

// ===== CART FUNCTIONS =====

// Add to cart
function addToCart(productId, quantity, productName) {
    if (isDebugMode) {
        console.log("Adding to cart:", productId, quantity, productName)
    }

    $.ajax({
        url: "/Cart/AddToCart",
        type: "POST",
        data: { productId: productId, quantity: quantity },
        success: (data) => {
            if (data.success) {
                $(".cart-count").text(data.cartCount)

                // Add animation to cart icon
                var cartCountElement = $(".cart-count")
                cartCountElement.addClass("cart-pulse")
                setTimeout(() => {
                    cartCountElement.removeClass("cart-pulse")
                }, 500)

                // Show notification with product name
                window.showNotification(data.productName + " đã được thêm vào giỏ hàng!", "success", "Giỏ hàng")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi thêm sản phẩm vào giỏ hàng.", "danger", "Lỗi")
        },
    })
}

// Update cart quantity
function updateCartQuantity(productId, quantity) {
    $.ajax({
        url: "/Cart/UpdateQuantity",
        type: "POST",
        data: { productId: productId, quantity: quantity },
        success: (data) => {
            if (data.success) {
                $("#item-total-" + productId).text(data.itemTotal.toLocaleString() + " VNĐ")
                $("#cart-total").text(data.cartTotal.toLocaleString() + " VNĐ")
                $(".cart-count").text(data.cartCount)

                window.showNotification(data.message, "success", "Giỏ hàng")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi cập nhật số lượng.", "danger", "Lỗi")
        },
    })
}

// Remove from cart
function removeFromCart(productId) {
    $.ajax({
        url: "/Cart/RemoveFromCart",
        type: "POST",
        data: { productId: productId },
        success: (data) => {
            if (data.success) {
                $("#cart-item-" + productId).fadeOut(300, function () {
                    $(this).remove()

                    if ($(".cart-item").length === 0) {
                        $("#cart-items-container").html(
                            '<div class="empty-cart text-center py-5">' +
                            '<i class="fas fa-shopping-cart fa-5x mb-3 text-muted"></i>' +
                            "<h3>Giỏ hàng trống</h3>" +
                            '<p class="text-muted">Giỏ hàng của bạn hiện đang trống.</p>' +
                            '<a href="/Product" class="btn btn-primary mt-3">' +
                            '<i class="fas fa-shopping-cart me-2"></i> Tiếp tục mua sắm' +
                            "</a>" +
                            "</div>",
                        )
                    }

                    $("#cart-total").text(data.cartTotal.toLocaleString() + " VNĐ")
                    $(".cart-count").text(data.cartCount)
                })

                window.showNotification(data.message, "success", "Giỏ hàng")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi xóa sản phẩm khỏi giỏ hàng.", "danger", "Lỗi")
        },
    })
}

// Apply promo code
function applyPromoCode(promoCode) {
    $.ajax({
        url: "/Cart/ApplyPromoCode",
        type: "POST",
        data: { promoCode: promoCode },
        success: (data) => {
            if (data.success) {
                $("#discount-percentage").text(data.discountPercentage + "%")
                $("#discount-amount").text("-" + data.discount.toLocaleString() + " VNĐ")
                $("#cart-total").text(data.total.toLocaleString() + " VNĐ")
                $("#discount-container").removeClass("d-none")

                window.showNotification(data.message, "success", "Mã khuyến mãi")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi áp dụng mã khuyến mãi.", "danger", "Lỗi")
        },
    })
}

// ===== WISHLIST FUNCTIONS =====

// Add to wishlist

function initializeWishlistButtons() {
    $('.btn-wishlist').each(function () {
        var $button = $(this);
        if ($button.data('is-wishlisted') === true || $button.hasClass('active-wishlist')) {
            $button.addClass('active-wishlist');
            $button.find('i').removeClass('far').addClass('fas');
            if ($button.attr('id') === 'addToWishlistDetails' && $button.find('span').length) { // Cập nhật text cho nút ở trang Details
                $button.find('span').text("Xóa khỏi Yêu thích");
            }
        } else {
            $button.removeClass('active-wishlist');
            $button.find('i').removeClass('fas').addClass('far');
            if ($button.attr('id') === 'addToWishlistDetails' && $button.find('span').length) { // Cập nhật text
                $button.find('span').text("Thêm vào Yêu thích");
            }
        }
    });
}

$(document).on('click', '.btn-wishlist', function (e) {
    e.preventDefault();
    var $button = $(this);
    var productId = $button.data('product-id');
    var productName = $button.data('product-name') || 'Sản phẩm';
    var ajaxUrl = $button.data('ajax-url');
    var loginUrl = $button.data('login-url');

    if (!productId || !ajaxUrl) {
        console.error('Wishlist button missing product-id or ajax-url.');
        window.showNotification("Lỗi cấu hình nút yêu thích.", "danger", "Lỗi");
        return;
    }

    $.ajax({
        url: ajaxUrl, // Should be /Wishlist/ToggleWishlist
        type: "POST",
        data: { productId: productId },
        success: function (data) {
            if (data.success) {
                if (data.added) {
                    $button.addClass("active-wishlist").find("i").removeClass("far").addClass("fas");
                    if ($button.attr('id') === 'addToWishlistDetails' && $button.find('span').length) {
                        $button.find('span').text("Xóa khỏi Yêu thích");
                    }
                    window.showNotification((data.productName || productName) + " đã được thêm vào yêu thích!", "success", "Yêu thích");
                } else {
                    $button.removeClass("active-wishlist").find("i").removeClass("fas").addClass("far");
                    if ($button.attr('id') === 'addToWishlistDetails' && $button.find('span').length) {
                        $button.find('span').text("Thêm vào Yêu thích");
                    }
                    window.showNotification((data.productName || productName) + " đã được xóa khỏi yêu thích!", "info", "Yêu thích");
                }
                if (typeof window.updateWishlistCount === 'function' && data.wishlistCount !== undefined) {
                    window.updateWishlistCount(data.wishlistCount);
                }
            } else {
                if (data.redirectToLogin && loginUrl) {
                    var returnUrl = encodeURIComponent(window.location.href);
                    if (confirm(data.message || 'Bạn cần đăng nhập để thực hiện. Đăng nhập ngay?')) {
                        window.location.href = loginUrl + '?returnUrl=' + returnUrl;
                    }
                } else {
                    window.showNotification(data.message || 'Không thể cập nhật danh sách yêu thích.', 'danger', 'Lỗi');
                }
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.error("Lỗi AJAX Wishlist:", textStatus, errorThrown, jqXHR.responseText);
            window.showNotification("Lỗi kết nối máy chủ. Vui lòng thử lại.", "danger", "Lỗi");
        }
    });
});

if (typeof window.updateWishlistCount === 'undefined') {
    window.updateWishlistCount = function (count) {
        // Ví dụ: $('.wishlist-header-count').text(count);
        console.log("Wishlist count: " + count);
    };
}

// Remove from wishlist
function removeFromWishlist(productId) {
    $.ajax({
        url: "/Wishlist/RemoveFromWishlist",
        type: "POST",
        data: { productId: productId },
        success: (data) => {
            if (data.success) {
                $("#wishlist-item-" + productId).fadeOut(300, function () {
                    $(this).remove()

                    if ($(".wishlist-item").length === 0) {
                        $("#wishlist-items-container").html(
                            '<div class="empty-wishlist text-center py-5">' +
                            '<i class="fas fa-heart fa-5x mb-3 text-muted"></i>' +
                            "<h3>Danh sách yêu thích trống</h3>" +
                            '<p class="text-muted">Danh sách yêu thích của bạn hiện đang trống.</p>' +
                            '<a href="/Product" class="btn btn-primary mt-3">' +
                            '<i class="fas fa-shopping-cart me-2"></i> Tiếp tục mua sắm' +
                            "</a>" +
                            "</div>",
                        )
                    }
                })

                window.showNotification(data.message, "success", "Danh sách yêu thích")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi xóa sản phẩm khỏi danh sách yêu thích.", "danger", "Lỗi")
        },
    })
}

// Move to cart
function moveToCart(productId) {
    $.ajax({
        url: "/Wishlist/MoveToCart",
        type: "POST",
        data: { productId: productId },
        success: (data) => {
            if (data.success) {
                $("#wishlist-item-" + productId).fadeOut(300, function () {
                    $(this).remove()

                    if ($(".wishlist-item").length === 0) {
                        $("#wishlist-items-container").html(
                            '<div class="empty-wishlist text-center py-5">' +
                            '<i class="fas fa-heart fa-5x mb-3 text-muted"></i>' +
                            "<h3>Danh sách yêu thích trống</h3>" +
                            '<p class="text-muted">Danh sách yêu thích của bạn hiện đang trống.</p>' +
                            '<a href="/Product" class="btn btn-primary mt-3">' +
                            '<i class="fas fa-shopping-cart me-2"></i> Tiếp tục mua sắm' +
                            "</a>" +
                            "</div>",
                        )
                    }

                    $(".cart-count").text(data.cartCount)
                })

                window.showNotification(data.message, "success", "Giỏ hàng")
            } else {
                window.showNotification(data.message, "danger", "Lỗi")
            }
        },
        error: () => {
            window.showNotification("Lỗi khi chuyển sản phẩm vào giỏ hàng.", "danger", "Lỗi")
        },
    })
}

// ===== ORDER FUNCTIONS =====

// Setup cancel order handler
function setupCancelOrderHandler() {
    // Xóa tất cả các event handler đã đăng ký trước đó
    $(document).off("click", ".cancel-order-btn")

    // Xử lý sự kiện click cho nút hủy đơn hàng
    $(document).on("click", ".cancel-order-btn", function (e) {
        e.preventDefault()
        e.stopPropagation() // Ngăn chặn sự kiện lan truyền

        var orderId = $(this).data("order-id")
        var button = $(this)

        if (isDebugMode) {
            console.log("Cancel order button clicked for order ID:", orderId)
        }

        if (confirm("Bạn có chắc chắn muốn hủy đơn hàng này không?")) {
            // Hiển thị trạng thái đang xử lý
            button.prop("disabled", true).html('<i class="fas fa-spinner fa-spin"></i> Đang xử lý...')

            // Gửi AJAX request
            $.ajax({
                url: "/Order/CancelOrder",
                type: "POST",
                data: { orderId: orderId },
                dataType: "json",
                success: (data) => {
                    if (isDebugMode) {
                        console.log("Cancel order response:", data)
                    }

                    if (data.success) {
                        window.showNotification(data.message, "success", "Đơn hàng")
                        // Reload the page after a short delay
                        setTimeout(() => {
                            location.reload()
                        }, 1500)
                    } else {
                        // Re-enable the button if there's an error
                        button.prop("disabled", false).html('<i class="fas fa-times"></i> Hủy đơn hàng')
                        window.showNotification(data.message, "danger", "Lỗi")
                    }
                },
                error: (xhr, status, error) => {
                    console.error("Cancel order error:", error)
                    console.error("Error details:", xhr.responseText)
                    // Re-enable the button if there's an error
                    button.prop("disabled", false).html('<i class="fas fa-times"></i> Hủy đơn hàng')
                    window.showNotification("Lỗi khi hủy đơn hàng: " + error, "danger", "Lỗi")
                },
            })
        }
    })
}

// ===== PRODUCT DETAILS FUNCTIONS =====

// Initialize product details page
function initializeProductDetailsPage() {
    // Update total price
    updateTotalPrice()

    // Setup image zoom
    setupImageZoom()
}

// Update total price
function updateTotalPrice() {
    if ($("#productPrice").length && $("#quantity").length && $("#totalPrice").length) {
        var price = Number.parseFloat($("#productPrice").data("price"))
        var discountedPrice = $("#productPrice").data("discounted-price")
        var currentPrice = discountedPrice ? Number.parseFloat(discountedPrice) : price
        var quantity = Number.parseInt($("#quantity").val())

        var total = currentPrice * quantity
        $("#totalPrice").text(total.toLocaleString() + " VNĐ")
    }
}

// Setup image zoom
function setupImageZoom() {
    $(".product-image-zoom").each(function () {
        var img = $(this)
        var zoomImg = img.data("zoom-image")

        img.wrap('<span class="zoom-container"></span>')

        img
            .parent()
            .mousemove(function (e) {
                var offset = $(this).offset()
                var x = e.pageX - offset.left
                var y = e.pageY - offset.top

                var containerWidth = $(this).width()
                var containerHeight = $(this).height()

                var xPercent = (x / containerWidth) * 100
                var yPercent = (y / containerHeight) * 100

                $(this).css("background-position", xPercent + "% " + yPercent + "%")
            })
            .mouseenter(function () {
                $(this).css({
                    "background-image": "url(" + zoomImg + ")",
                    "background-size": "200%",
                    "background-repeat": "no-repeat",
                })
                img.css("opacity", 0)
            })
            .mouseleave(function () {
                $(this).css("background-image", "none")
                img.css("opacity", 1)
            })
    })
}

// Change main image
function changeMainImage(src) {
    $("#mainImage").attr("src", src)
    $("#mainImage").attr("data-zoom-image", src)
    $(".thumbnail").removeClass("active")
    $('.thumbnail[src="' + src + '"]').addClass("active")
}

// Setup rating stars
function setupRatingStars() {
    $(".rating-stars i").hover(
        function () {
            var rating = $(this).data("rating")
            for (var i = 1; i <= 5; i++) {
                if (i <= rating) {
                    $('.rating-stars i[data-rating="' + i + '"]')
                        .removeClass("far")
                        .addClass("fas")
                } else {
                    $('.rating-stars i[data-rating="' + i + '"]')
                        .removeClass("fas")
                        .addClass("far")
                }
            }
        },
        () => {
            var selectedRating = $('input[name="rating"]:checked').val()
            if (selectedRating) {
                for (var i = 1; i <= 5; i++) {
                    if (i <= selectedRating) {
                        $('.rating-stars i[data-rating="' + i + '"]')
                            .removeClass("far")
                            .addClass("fas")
                    } else {
                        $('.rating-stars i[data-rating="' + i + '"]')
                            .removeClass("fas")
                            .addClass("far")
                    }
                }
            } else {
                $(".rating-stars i").removeClass("fas").addClass("far")
            }
        },
    )

    $(".rating-stars i").click(function () {
        var rating = $(this).data("rating")
        $("#rating-" + rating).prop("checked", true)

        for (var i = 1; i <= 5; i++) {
            if (i <= rating) {
                $('.rating-stars i[data-rating="' + i + '"]')
                    .removeClass("far")
                    .addClass("fas")
            } else {
                $('.rating-stars i[data-rating="' + i + '"]')
                    .removeClass("fas")
                    .addClass("far")
            }
        }
    })
}

// Setup share buttons
function setupShareButtons() {
    $(".share-button").click(function () {
        var platform = $(this).data("platform")
        var url = encodeURIComponent(window.location.href)
        var title = encodeURIComponent($(".product-title").text())

        switch (platform) {
            case "facebook":
                window.open("https://www.facebook.com/sharer/sharer.php?u=" + url, "_blank")
                break
            case "twitter":
                window.open("https://twitter.com/intent/tweet?url=" + url + "&text=" + title, "_blank")
                break
            case "pinterest":
                var image = encodeURIComponent($("#mainImage").attr("src"))
                window.open(
                    "https://pinterest.com/pin/create/button/?url=" + url + "&media=" + image + "&description=" + title,
                    "_blank",
                )
                break
            case "email":
                window.location.href = "mailto:?subject=" + title + "&body=Check out this product: " + url
                break
        }
    })
}

// ===== ACCOUNT FUNCTIONS =====

// Setup account functionality
function setupAccountFunctionality() {
    // Toggle password visibility
    $(".password-toggle").click(function () {
        var target = $(this).data("target")
        var input = $(target)

        if (input.attr("type") === "password") {
            input.attr("type", "text")
            $(this).find("i").removeClass("fa-eye").addClass("fa-eye-slash")
        } else {
            input.attr("type", "password")
            $(this).find("i").removeClass("fa-eye-slash").addClass("fa-eye")
        }
    })

    // Password strength meter
    $("#Password").on("keyup", function () {
        var password = $(this).val()
        var strength = 0

        if (password.length >= 8) strength += 1
        if (password.match(/[a-z]+/)) strength += 1
        if (password.match(/[A-Z]+/)) strength += 1
        if (password.match(/[0-9]+/)) strength += 1
        if (password.match(/[^a-zA-Z0-9]+/)) strength += 1

        var strengthBar = $(".password-strength")

        switch (strength) {
            case 0:
            case 1:
                strengthBar.css("width", "20%").css("background-color", "#ff4757")
                break
            case 2:
                strengthBar.css("width", "40%").css("background-color", "#ffa502")
                break
            case 3:
                strengthBar.css("width", "60%").css("background-color", "#ffcc00")
                break
            case 4:
                strengthBar.css("width", "80%").css("background-color", "#26de81")
                break
            case 5:
                strengthBar.css("width", "100%").css("background-color", "#2ed573")
                break
        }
    })

    // Check if passwords match
    $("#ConfirmPassword").on("keyup", function () {
        var password = $("#Password").val()
        var confirmPassword = $(this).val()

        if (password !== confirmPassword) {
            $("#password-match-error").text("Passwords do not match")
        } else {
            $("#password-match-error").text("")
        }
    })

    // Check username availability
    $("#UserName").on("blur", function () {
        var username = $(this).val()

        if (username) {
            $.ajax({
                url: "/Account/CheckUsernameAvailability",
                type: "POST",
                data: { username: username },
                success: (available) => {
                    if (!available) {
                        $("#UserName").next(".text-danger").text("This username is already taken")
                    } else {
                        $("#UserName").next(".text-danger").text("")
                    }
                },
            })
        }
    })

    // Check email availability
    $("#Email").on("blur", function () {
        var email = $(this).val()

        if (email) {
            $.ajax({
                url: "/Account/CheckEmailAvailability",
                type: "POST",
                data: { email: email },
                success: (available) => {
                    if (!available) {
                        $("#Email").next(".text-danger").text("This email is already registered")
                    } else {
                        $("#Email").next(".text-danger").text("")
                    }
                },
            })
        }
    })

    // Form validation
    $("#registerForm").submit((e) => {
        var password = $("#Password").val()
        var confirmPassword = $("#ConfirmPassword").val()

        if (password !== confirmPassword) {
            e.preventDefault()
            $("#password-match-error").text("Passwords do not match")
            return false
        }

        if (!$("#termsAgreement").is(":checked")) {
            e.preventDefault()
            alert("You must agree to the Terms of Service and Privacy Policy")
            return false
        }

        return true
    })
}

// ===== CART FUNCTIONS FROM CART.JS =====

function setupCartFunctionality() {
    // Remove item
    $(".remove-item").click(function () {
        var row = $(this).closest("tr")
        var productId = row.data("product-id")

        $.ajax({
            url: "/Cart/RemoveFromCart",
            type: "POST",
            data: { productId: productId },
            success: (data) => {
                if (data.success) {
                    row.fadeOut(300, function () {
                        $(this).remove()

                        // Update cart totals
                        $(".cart-subtotal, .cart-total").text(data.cartTotal.toLocaleString("vi-VN") + " VNĐ")
                        $(".cart-count").text(data.cartCount)

                        // If cart is empty, reload the page
                        if (data.cartCount === 0) {
                            location.reload()
                        }
                    })

                    window.showNotification("Sản phẩm đã được xóa khỏi giỏ hàng.", "success", "Giỏ hàng")
                }
            },
            error: () => {
                window.showNotification("Lỗi khi xóa sản phẩm khỏi giỏ hàng.", "danger", "Lỗi")
            },
        })
    })

    // Apply promo code
    $("#applyPromoCode").click(() => {
        var promoCode = $("#promoCode").val()

        if (!promoCode) {
            window.showNotification("Vui lòng nhập mã giảm giá.", "warning", "Mã giảm giá")
            return
        }

        $.ajax({
            url: "/Cart/ApplyPromoCode",
            type: "POST",
            data: { promoCode: promoCode },
            success: (data) => {
                if (data.success) {
                    // Add discount row if it doesn't exist
                    if ($(".discount-row").length === 0) {
                        var discountRow =
                            '<tr class="discount-row">' +
                            '<td>Giảm giá (<span class="discount-percentage">' +
                            data.discountPercentage +
                            "</span>%)</td>" +
                            '<td class="text-end text-success cart-discount">-' +
                            data.discount.toLocaleString("vi-VN") +
                            " VNĐ</td>" +
                            "</tr>"

                        $(".cart-summary-table tbody tr:first-child").after(discountRow)
                    } else {
                        $(".discount-percentage").text(data.discountPercentage)
                        $(".cart-discount").text("-" + data.discount.toLocaleString("vi-VN") + " VNĐ")
                    }

                    // Update total
                    $(".cart-total").text(data.total.toLocaleString("vi-VN") + " VNĐ")

                    // Disable promo code input
                    $("#promoCode").prop("disabled", true)
                    $("#applyPromoCode").prop("disabled", true).text("Đã áp dụng")

                    // Add success message
                    $(".promo-code").append(
                        '<div class="text-success mt-1 small"><i class="fas fa-check-circle"></i> Mã giảm giá đã được áp dụng thành công!</div>',
                    )

                    window.showNotification("Mã giảm giá đã được áp dụng thành công!", "success", "Mã giảm giá")
                } else {
                    window.showNotification(data.message, "danger", "Lỗi")
                }
            },
            error: () => {
                window.showNotification("Lỗi khi áp dụng mã giảm giá.", "danger", "Lỗi")
            },
        })
    })
}

// ===== DEBUG FUNCTIONS =====

// Toggle debug mode
function toggleDebugMode() {
    isDebugMode = !isDebugMode
    console.log("Debug mode:", isDebugMode ? "ON" : "OFF")

    if (isDebugMode) {
        setupDebugListeners()
    }
}

// Setup debug listeners
function setupDebugListeners() {
    console.log("Setting up debug listeners")

    // Debug cho nút thêm vào giỏ hàng
    $(document).on("click", "#addToCartBtn", function () {
        console.log("Debug: Add to cart button clicked")
        console.log("Product ID:", $(this).data("product-id"))
        console.log("Quantity:", $("#quantity").val())
    })

    // Debug cho nút hủy đơn hàng
    $(document).on("click", ".cancel-order-btn", function () {
        console.log("Debug: Cancel order button clicked")
        console.log("Order ID:", $(this).data("order-id"))
    })

    // Debug cho thông báo
    var originalShowNotification = window.showNotification
    window.showNotification = (message, type, title) => {
        console.log("Debug: Showing notification")
        console.log("Message:", message)
        console.log("Type:", type)
        console.log("Title:", title)
        return originalShowNotification(message, type, title)
    }

    // Debug cho AJAX requests
    $(document).ajaxSend((event, jqXHR, settings) => {
        console.log("Debug: AJAX request sent")
        console.log("URL:", settings.url)
        console.log("Type:", settings.type)
        console.log("Data:", settings.data)
    })

    $(document).ajaxSuccess((event, jqXHR, settings, data) => {
        console.log("Debug: AJAX request succeeded")
        console.log("URL:", settings.url)
        console.log("Response:", data) 
    })

    $(document).ajaxError((event, jqXHR, settings, error) => {
        console.log("Debug: AJAX request failed")
        console.log("URL:", settings.url)
        console.log("Error:", error)
        console.log("Response:", jqXHR.responseText)
    })

    // Kiểm tra xem có nút hủy đơn hàng nào không
    var cancelButtons = $(".cancel-order-btn")
    if (cancelButtons.length) {
        console.log("Debug: Found " + cancelButtons.length + " cancel buttons on page load")
        cancelButtons.each(function () {
            console.log("Debug: Cancel button with order ID: " + $(this).data("order-id"))
        })
    } else {
        console.log("Debug: No cancel buttons found on page load")
    }
}
